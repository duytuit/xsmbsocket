using Microsoft.Extensions.Caching.Distributed;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Shares.MysqlHelper
{
    public static class AdoRelationQuery
    {
        /// <summary>
        /// Truy vấn dữ liệu từ cơ sở dữ liệu MySQL với khả năng chọn cột, phân trang, lọc, sắp xếp và tải các quan hệ liên quan.
        /// Hỗ trợ đếm tổng số bản ghi (nếu được yêu cầu) và lưu/truy xuất kết quả vào Redis cache.
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến cơ sở dữ liệu MySQL.</param>
        /// <param name="tableName">Tên bảng chính để truy vấn.</param>
        /// <param name="columns">Danh sách các cột cần lấy dữ liệu.</param>
        /// <param name="offset">Vị trí bắt đầu lấy dữ liệu (phân trang).</param>
        /// <param name="limit">Số lượng bản ghi cần lấy (phân trang).</param>
        /// <param name="whereEquals">Tập điều kiện lọc theo giá trị chính xác (column = value).</param>
        /// <param name="whereLikes">Tập điều kiện lọc theo LIKE (column LIKE '%value%').</param>
        /// <param name="whereInList">Tập điều kiện lọc theo danh sách (column IN (...)).</param>
        /// <param name="dateRangeList">Danh sách điều kiện lọc theo khoảng thời gian (column BETWEEN from AND to).</param>
        /// <param name="orderByList">Danh sách cột cần sắp xếp (có thể kèm ASC/DESC).</param>
        /// <param name="relations">Danh sách các quan hệ cần load (quan hệ 1-1 hoặc 1-n).</param>
        /// <param name="redisCache">Dịch vụ Redis để lưu/truy xuất cache.</param>
        /// <param name="redisKey">Khóa Redis dùng để cache dữ liệu.</param>
        /// <param name="redisKeyDuration">Thời gian sống của cache trong Redis.</param>
        /// <param name="includeCount">Nếu true, thực hiện truy vấn để lấy tổng số bản ghi (Count).</param>
        /// <param name="cancellationToken">Token để hủy thao tác bất đồng bộ nếu cần.</param>
        /// <returns>
        /// Một đối tượng chứa:
        /// - Count: Tổng số bản ghi (chỉ có giá trị nếu includeCount = true)
        /// - Data: Danh sách kết quả dưới dạng List&lt;ExpandoObject&gt;
        /// </returns>
        public static async Task<object> WithRelationsAdoAsync(
        string connectionString,
        string tableName,
        string[] columns,
        int? offset = null,
        int? limit = null,
        Dictionary<string, object> whereEquals = null,
        Dictionary<string, string> whereLikes = null,
        Dictionary<string, IEnumerable<object>> whereInList = null,
        List<(string Sql, object[] Params)> whereCustom = null,
        List<(string Field, DateTime From, DateTime To)> dateRangeList = null,
        List<string> orderByList = null,
        IEnumerable<AdoRelation> relations = null,
        RedisService redisCache = null,
        string redisKey = null,
        TimeSpan? redisKeyDuration = null,
        bool includeCount = false,
        CancellationToken cancellationToken = default)
        {
            int count = 0;

            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            // Nếu cần đếm tổng số bản ghi
            if (includeCount)
            {
                count = await SqlHelpers.ExecuteCountCommandAsync(
                                      conn, tableName, whereEquals, whereLikes, whereInList, whereCustom, dateRangeList, cancellationToken);
            }

            // Kiểm tra Redis cache
            if (!string.IsNullOrEmpty(redisKey) && redisKeyDuration.HasValue && redisCache != null)
            {
                var cachedJson = await redisCache.GetAsync(redisKey, cancellationToken);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cachedData = JsonSerializer.Deserialize<List<ExpandoObject>>(cachedJson);
                    if (cachedData != null)
                        return new { Count = count, Data = cachedData };
                }
            }

            using var baseCmd = await SqlHelpers.BuildBaseCommandAsync(
                conn, tableName, columns, offset, limit,
                whereEquals, whereLikes, whereInList, whereCustom, dateRangeList, orderByList, cancellationToken);

            var baseList = (await SqlHelpers.ExecuteQueryAsync(baseCmd, cancellationToken))
                .Cast<IDictionary<string, object>>().ToList();

            await LoadRelationsRecursiveAsync(conn, baseList, relations, cancellationToken);

            var result = baseList.Select(r => (ExpandoObject)r).ToList();

            // Lưu vào Redis nếu cần
            if (!string.IsNullOrEmpty(redisKey) && redisKeyDuration.HasValue && redisCache != null)
            {
                var json = JsonSerializer.Serialize(result);
                await redisCache.SetAsync(redisKey, json, redisKeyDuration, cancellationToken);
            }

            return new { Count = count, Data = result };
        }

        private static async Task LoadRelationsRecursiveAsync(
            MySqlConnection conn,
            List<IDictionary<string, object>> parentList,
            IEnumerable<AdoRelation> relations,
            CancellationToken cancellationToken)
        {
            foreach (var relation in relations ?? Enumerable.Empty<AdoRelation>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parentKeys = parentList
                    .Select(p => p[relation.ParentKey])
                    .Where(v => v != null)
                    .Distinct()
                    .ToList();

                if (parentKeys.Count == 0)
                    continue;

                List<IDictionary<string, object>> childList;

               
                var cmd = await SqlHelpers.BuildSelectInCommandAsync(conn, relation.Table, relation.Columns, relation.KeyName, parentKeys, cancellationToken);
                childList = (await SqlHelpers.ExecuteQueryAsync(cmd, cancellationToken))
                    .Cast<IDictionary<string, object>>().ToList();

                var childLookup = childList
                    .Where(c => c.ContainsKey(relation.ForeignKey))
                    .GroupBy(c => Convert.ToInt64(c[relation.ForeignKey]))
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var parent in parentList)
                {
                    var key = Convert.ToInt64(parent[relation.ParentKey]);
                    childLookup.TryGetValue(key, out var relatedItems);

                    if (relation.IsCollection)
                    {
                        parent[relation.Name] = relatedItems != null
                        ? relatedItems.Select(x => (ExpandoObject)x).ToList()
                        : new List<ExpandoObject>();
                    }
                    else
                    {
                        parent[relation.Name] = relatedItems != null ? ((ExpandoObject)relatedItems.FirstOrDefault()) : new ExpandoObject();
                    }
                }

                if (relation.SubRelations?.Any() == true)
                {
                    var allChildren = childList.Select(c => (IDictionary<string, object>)c).ToList();
                    await LoadRelationsRecursiveAsync(conn, allChildren, relation.SubRelations, cancellationToken);
                }
            }
        }
    }
}
