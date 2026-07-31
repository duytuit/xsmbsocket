using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Shares.MysqlHelper;

namespace xsmbsocket.Shares.SqlServerHelper
{
    public static class AdoRelationQuerySqlServer
    {
        #region WithRelationsAdoAsync
        /// <summary>
        /// Truy vấn dữ liệu cha + quan hệ con (1-n, 1-1) + cache Redis + alias + join
        /// </summary>
        public static async Task<object> WithRelationsAdoAsync(
         string connectionString,
         string tableNameWithAlias,
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
         List<(string Sql, object[] Params)> joinsList = null,
         RedisService redisCache = null,
         string redisKey = null,
         TimeSpan? redisKeyDuration = null,
         bool includeCount = false,
         CancellationToken cancellationToken = default)
        {
            int count = 0;

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            // Tách table name và alias
            var parts = tableNameWithAlias.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string tableBaseName = parts[0];
            string alias = parts.Length > 1 ? parts[1] : null;

            // Đếm tổng số
            if (includeCount)
            {
                count = await SqlServerHelpers.ExecuteCountCommandAsync(
                    conn, tableNameWithAlias, whereEquals, whereLikes, whereInList, whereCustom, dateRangeList, joinsList, cancellationToken);
            }

            // Check Redis
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

            // Build command
            using var cmd = await SqlServerHelpers.BuildBaseCommandAsync(
                conn,
                tableNameWithAlias,
                columns,
                joinsList,
                offset,
                limit,
                whereEquals,
                whereLikes,
                whereInList,
                whereCustom,
                dateRangeList,
                orderByList,
                cancellationToken
            );

            // Execute query cha
            var baseList = (await SqlServerHelpers.ExecuteQueryAsync(cmd, cancellationToken))
                .Cast<IDictionary<string, object>>()
                .ToList();

            // Load relations (1-1 hoặc 1-n)
            if (relations != null && relations.Any())
            {
                await LoadRelationsRecursiveAsync(conn, baseList, relations, cancellationToken);
            }

            var result = baseList.Select(r => (ExpandoObject)r).ToList();

            // Lưu cache
            if (!string.IsNullOrEmpty(redisKey) && redisKeyDuration.HasValue && redisCache != null)
            {
                var json = JsonSerializer.Serialize(result);
                await redisCache.SetAsync(redisKey, json, redisKeyDuration.Value, cancellationToken);
            }

            return new { Count = count, Data = result };
        }
        #endregion

        #region LoadRelationsRecursiveAsync
        private static async Task LoadRelationsRecursiveAsync(
            SqlConnection conn,
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

                // Build child command
                var cmd = await SqlServerHelpers.BuildSelectInCommandAsync(conn, relation.Table, relation.Columns, relation.KeyName, parentKeys, cancellationToken);
                var childList = (await SqlServerHelpers.ExecuteQueryAsync(cmd, cancellationToken))
                    .Cast<IDictionary<string, object>>()
                    .ToList();

                // Map child -> parent
                var childLookup = childList
                    .Where(c => c.ContainsKey(relation.ForeignKey))
                    .GroupBy(c => Convert.ToInt64(c[relation.ForeignKey]))
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var parent in parentList)
                {
                    if (!parent.ContainsKey(relation.ParentKey) || parent[relation.ParentKey] == null)
                        continue;

                    var key = Convert.ToInt64(parent[relation.ParentKey]);
                    childLookup.TryGetValue(key, out var relatedItems);

                    parent[relation.Name] = relation.IsCollection
                        ? (relatedItems?.Select(x => (ExpandoObject)x).ToList() ?? new List<ExpandoObject>())
                        : (ExpandoObject)(relatedItems?.FirstOrDefault() ?? null);
                }

                // Recursive sub-relations
                if (relation.SubRelations?.Any() == true)
                {
                    var allChildren = childList.Select(c => (IDictionary<string, object>)c).ToList();
                    await LoadRelationsRecursiveAsync(conn, allChildren, relation.SubRelations, cancellationToken);
                }
            }
        }
        #endregion
    }
}
