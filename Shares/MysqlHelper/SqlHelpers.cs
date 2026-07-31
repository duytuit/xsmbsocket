using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Shares.MysqlHelper
{
    public static class SqlHelpers
    {
        public static async Task<MySqlCommand> BuildBaseCommandAsync(
         MySqlConnection connection,
         string tableName,
         string[] fields,
         int? skip = null,
         int? take = null,
         Dictionary<string, object> whereEquals = null,
         Dictionary<string, string> whereLikes = null,
         Dictionary<string, IEnumerable<object>> whereInList = null,
         List<(string Sql, object[] Params)> whereCustom = null,
         List<(string Field, DateTime From, DateTime To)> dateRangeList = null,
         List<string> orderByList = null,
         CancellationToken cancellationToken = default)
        {
            var cmd = new MySqlCommand();
            cmd.Connection = connection;

            // Kiểm tra có deleted_at
            cmd.CommandText = $@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @table AND COLUMN_NAME = 'deleted_at'";
            cmd.Parameters.AddWithValue("@table", tableName);
            var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken));
            bool hasDeletedAt = count > 0;

            var whereClauses = new List<string>();

            if (hasDeletedAt)
                whereClauses.Add("deleted_at IS NULL");

            // WHERE EQUALS
            if (whereEquals != null)
            {
                foreach (var kvp in whereEquals)
                {
                    string paramName = $"@{kvp.Key}";
                    whereClauses.Add($"{kvp.Key} = {paramName}");
                    cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
                }
            }

            // WHERE LIKE
            if (whereLikes != null)
            {
                foreach (var kvp in whereLikes)
                {
                    string paramName = $"@like_{kvp.Key}";
                    whereClauses.Add($"{kvp.Key} LIKE {paramName}");
                    cmd.Parameters.AddWithValue(paramName, $"%{kvp.Value}%");
                }
            }

            // WHERE IN
            if (whereInList != null)
            {
                foreach (var kvp in whereInList)
                {
                    var paramNames = new List<string>();
                    int index = 0;
                    foreach (var value in kvp.Value)
                    {
                        string paramName = $"@in_{kvp.Key}_{index++}";
                        paramNames.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, value);
                    }
                    whereClauses.Add($"{kvp.Key} IN ({string.Join(", ", paramNames)})");
                }
            }
            // WHERE Custom
            if (whereCustom != null)
            {
                int customIndex = 0;
                foreach (var (sql, paramValues) in whereCustom)
                {
                    var parts = sql.Split('?');
                    var sqlWithParams = "";

                    for (int i = 0; i < paramValues.Length; i++)
                    {
                        string paramName = $"@customParam_{customIndex}";
                        cmd.Parameters.AddWithValue(paramName, paramValues[i]);
                        sqlWithParams += parts[i] + paramName;
                        customIndex++;
                    }

                    // Add the last part if exists
                    if (parts.Length > paramValues.Length)
                    {
                        sqlWithParams += parts.Last();
                    }

                    whereClauses.Add(sqlWithParams);
                }
            }
            // WHERE Date Range
            if (dateRangeList != null)
            {
                for (int i = 0; i < dateRangeList.Count; i++)
                {
                    var range = dateRangeList[i];
                    string fromParam = $"@dateFrom{i}";
                    string toParam = $"@dateTo{i}";
                    whereClauses.Add($"{range.Field} BETWEEN {fromParam} AND {toParam}");
                    cmd.Parameters.AddWithValue(fromParam, range.From);
                    cmd.Parameters.AddWithValue(toParam, range.To);
                }
            }

            // Compose final parts
            string whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
            string orderClause = (orderByList != null && orderByList.Count > 0) ? "ORDER BY " + string.Join(", ", orderByList) : "";
            string pagingClause =(take.HasValue ? $"LIMIT {Math.Min(take.Value, 1000)}" : "") +
                                 (skip.HasValue ? $" OFFSET {skip.Value}" : "");

            string fieldList = string.Join(", ", fields);

            cmd.CommandText = $@"
            SELECT {fieldList}
            FROM {tableName}
            {whereClause}
            {orderClause}
            {pagingClause}".Trim();

            return cmd;
        }

        public static async Task<MySqlCommand> BuildSelectInCommandAsync(MySqlConnection conn, string tableName, string[] fields, string keyField, List<object> ids, CancellationToken cancellationToken = default)
        {
            string fieldList = string.Join(", ", fields);
            var cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText =$@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @table AND COLUMN_NAME = 'deleted_at'";
            cmd.Parameters.AddWithValue("@table", tableName);
            var count = (long)await cmd.ExecuteScalarAsync(cancellationToken);
            bool hasDeletedAt = count > 0;
            var parameters = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                var paramName = "@id" + i;
                parameters.Add(paramName);
                cmd.Parameters.AddWithValue(paramName, ids[i]);
            }

            string whereIn = $"{keyField} IN ({string.Join(", ", parameters)})";
            string whereClause = hasDeletedAt
                ? $"WHERE {whereIn} AND deleted_at IS NULL"
                : $"WHERE {whereIn}";
            cmd.CommandText = $"SELECT {fieldList} FROM {tableName} {whereClause}";
            return cmd;
        }

        public static async Task<List<ExpandoObject>> ExecuteQueryAsync(MySqlCommand cmd, CancellationToken cancellationToken = default)
        {
            var result = new List<ExpandoObject>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = new ExpandoObject() as IDictionary<string, object>;
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);
                }
                result.Add((ExpandoObject)row);
            }
            return result;
        }
        public static async Task<int> ExecuteCountCommandAsync(
        MySqlConnection conn,
        string tableName,
        Dictionary<string, object> whereEquals = null,
        Dictionary<string, string> whereLikes = null,
        Dictionary<string, IEnumerable<object>> whereInList = null,
        List<(string Sql, object[] Params)> whereCustom = null,
        List<(string Field, DateTime From, DateTime To)> dateRangeList = null,
        CancellationToken cancellationToken = default)
        {
            var whereClauses = new List<string>();
            var cmd = conn.CreateCommand();

            if (whereEquals != null)
            {
                foreach (var kv in whereEquals)
                {
                    var paramName = $"@eq_{kv.Key}";
                    whereClauses.Add($"`{kv.Key}` = {paramName}");
                    cmd.Parameters.AddWithValue(paramName, kv.Value);
                }
            }

            if (whereLikes != null)
            {
                foreach (var kv in whereLikes)
                {
                    var paramName = $"@like_{kv.Key}";
                    whereClauses.Add($"`{kv.Key}` LIKE {paramName}");
                    cmd.Parameters.AddWithValue(paramName, $"%{kv.Value}%");
                }
            }

            if (whereInList != null)
            {
                foreach (var kv in whereInList)
                {
                    var paramNames = kv.Value.Select((v, i) => $"@in_{kv.Key}_{i}").ToList();
                    whereClauses.Add($"`{kv.Key}` IN ({string.Join(", ", paramNames)})");

                    int index = 0;
                    foreach (var val in kv.Value)
                        cmd.Parameters.AddWithValue(paramNames[index++], val);
                }
            }
            if (whereCustom != null)
            {
                int customIndex = 0;
                foreach (var (sql, paramValues) in whereCustom)
                {
                    var parts = sql.Split('?');
                    var sqlWithParams = "";

                    for (int i = 0; i < paramValues.Length; i++)
                    {
                        string paramName = $"@customParam_{customIndex}";
                        cmd.Parameters.AddWithValue(paramName, paramValues[i]);
                        sqlWithParams += parts[i] + paramName;
                        customIndex++;
                    }

                    // Add the last part if exists
                    if (parts.Length > paramValues.Length)
                    {
                        sqlWithParams += parts.Last();
                    }

                    whereClauses.Add(sqlWithParams);
                }
            }
            if (dateRangeList != null)
            {
                foreach (var range in dateRangeList)
                {
                    var fromParam = $"@from_{range.Field}";
                    var toParam = $"@to_{range.Field}";
                    whereClauses.Add($"`{range.Field}` BETWEEN {fromParam} AND {toParam}");
                    cmd.Parameters.AddWithValue(fromParam, range.From);
                    cmd.Parameters.AddWithValue(toParam, range.To);
                }
            }

            var whereSql = whereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";
            cmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}` {whereSql}";

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }
    }

}
