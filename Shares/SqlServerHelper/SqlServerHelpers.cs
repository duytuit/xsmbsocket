using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Shares.SqlServerHelper
{
    public static class SqlServerHelpers
    {
        #region BuildBaseCommandAsync
        /// <summary>
        /// Build SQL command hỗ trợ alias, join, where, paging
        /// </summary>
        public static async Task<SqlCommand> BuildBaseCommandAsync(
    SqlConnection connection,
    string tableNameWithAlias,
    string[] fields,
    List<(string Sql, object[] Params)> joinsList = null,
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
            var cmd = new SqlCommand();
            cmd.Connection = connection;

            // Tách tên bảng và alias
            var parts = tableNameWithAlias.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string tableBaseName = parts[0];
            string alias = parts.Length > 1 ? parts[1] : null;

            // Helper thêm alias vào cột
            string Col(string col)
            {
                return col.Contains(".") ? col : (alias != null ? $"{alias}.[{col}]" : $"[{col}]");
            }

            // Kiểm tra deleted_at
            cmd.CommandText = @"
        SELECT COUNT(*) 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = @table AND COLUMN_NAME = 'deleted_at'";
            cmd.Parameters.AddWithValue("@table", tableBaseName);
            bool hasDeletedAt = ((int)await cmd.ExecuteScalarAsync(cancellationToken)) > 0;

            // WHERE clause
            var whereClauses = new List<string>();
            if (hasDeletedAt)
                whereClauses.Add(Col("deleted_at") + " IS NULL");

            // WHERE EQUALS
            if (whereEquals != null)
            {
                foreach (var kv in whereEquals)
                {
                    string paramName = $"@eq_{kv.Key.Split('.').Last()}";
                    whereClauses.Add($"{kv.Key} = {paramName}");
                    cmd.Parameters.AddWithValue(paramName, kv.Value ?? DBNull.Value);
                }
            }

            // WHERE LIKE
            if (whereLikes != null)
            {
                foreach (var kv in whereLikes)
                {
                    string paramName = $"@like_{kv.Key.Split('.').Last()}";
                    whereClauses.Add($"{kv.Key} LIKE {paramName}");
                    cmd.Parameters.AddWithValue(paramName, $"%{kv.Value}%");
                }
            }

            // WHERE IN
            if (whereInList != null)
            {
                foreach (var kv in whereInList)
                {
                    var paramNames = new List<string>();
                    int idx = 0;
                    foreach (var val in kv.Value)
                    {
                        string param = $"@in_{kv.Key.Split('.').Last()}_{idx++}";
                        paramNames.Add(param);
                        cmd.Parameters.AddWithValue(param, val ?? DBNull.Value);
                    }
                    if (paramNames.Count > 0)
                        whereClauses.Add($"{kv.Key} IN ({string.Join(", ", paramNames)})");
                }
            }

            // WHERE CUSTOM
            if (whereCustom != null)
            {
                int customIndex = 0;
                foreach (var (sql, vals) in whereCustom)
                {
                    var partsSql = sql.Split('?');
                    string sqlWithParams = "";
                    for (int i = 0; i < vals.Length; i++)
                    {
                        string param = $"@custom_{customIndex++}";
                        cmd.Parameters.AddWithValue(param, vals[i] ?? DBNull.Value);
                        sqlWithParams += partsSql[i] + param;
                    }
                    if (partsSql.Length > vals.Length)
                        sqlWithParams += partsSql.Last();
                    whereClauses.Add(sqlWithParams);
                }
            }

            // WHERE DATE RANGE
            if (dateRangeList != null)
            {
                for (int i = 0; i < dateRangeList.Count; i++)
                {
                    var r = dateRangeList[i];
                    string paramFrom = $"@from_{r.Field.Split('.').Last()}_{i}";
                    string paramTo = $"@to_{r.Field.Split('.').Last()}_{i}";
                    whereClauses.Add($"{r.Field} BETWEEN {paramFrom} AND {paramTo}");
                    cmd.Parameters.AddWithValue(paramFrom, r.From);
                    cmd.Parameters.AddWithValue(paramTo, r.To);
                }
            }

            string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            // JOIN
            string joinSql = "";
            if (joinsList != null && joinsList.Count > 0)
            {
                int joinIndex = 0;
                var joinClauses = new List<string>();
                foreach (var (sql, vals) in joinsList)
                {
                    if (string.IsNullOrWhiteSpace(sql)) continue;
                    if (vals != null && vals.Length > 0)
                    {
                        var partsSql = sql.Split('?');
                        string sqlWithParams = "";
                        for (int i = 0; i < vals.Length; i++)
                        {
                            string param = $"@join_{joinIndex++}";
                            cmd.Parameters.AddWithValue(param, vals[i] ?? DBNull.Value);
                            sqlWithParams += partsSql[i] + param;
                        }
                        if (partsSql.Length > vals.Length)
                            sqlWithParams += partsSql.Last();
                        joinClauses.Add(sqlWithParams);
                    }
                    else
                    {
                        joinClauses.Add(sql);
                    }
                }
                joinSql = string.Join(" ", joinClauses);
            }

            // Fields
            string fieldList = string.Join(", ", fields.Select(f => f.Contains(".") ? f : (alias != null ? $"{alias}.[{f}]" : $"[{f}]")));

            // ORDER BY
            string orderSql = "";
            if (orderByList != null && orderByList.Count > 0)
            {
                orderSql = "ORDER BY " + string.Join(", ", orderByList.Select(c => c.Contains(".") ? c : (alias != null ? $"{alias}.{c}" : c)));
            }

            // Paging
            string pagingSql = "";
            if (skip.HasValue || take.HasValue)
            {
                pagingSql = "OFFSET " + (skip ?? 0) + " ROWS";
                if (take.HasValue)
                    pagingSql += $" FETCH NEXT {Math.Min(take.Value, 1000)} ROWS ONLY";
            }

            cmd.CommandText = $@"
SELECT {fieldList}
FROM {tableNameWithAlias} {joinSql}
{whereSql}
{orderSql}
{pagingSql}".Trim();

            return cmd;
        }
        #endregion

        #region BuildSelectInCommandAsync
        public static async Task<SqlCommand> BuildSelectInCommandAsync(
             SqlConnection conn,
             string tableName,
             string[] fields,
             string keyField,
             List<object> ids,
             CancellationToken cancellationToken = default)
        {
            string fieldList = string.Join(", ", fields.Select(f => $"[{f}]"));
            var cmd = new SqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @table AND COLUMN_NAME = 'deleted_at'";
            cmd.Parameters.AddWithValue("@table", tableName);
            var count = (int)await cmd.ExecuteScalarAsync(cancellationToken);
            bool hasDeletedAt = count > 0;

            var parameters = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                var paramName = "@id" + i;
                parameters.Add(paramName);
                cmd.Parameters.AddWithValue(paramName, ids[i]);
            }

            string whereIn = $"[{keyField}] IN ({string.Join(", ", parameters)})";
            string whereClause = hasDeletedAt
                ? $"WHERE {whereIn} AND deleted_at IS NULL"
                : $"WHERE {whereIn}";

            cmd.CommandText = $"SELECT {fieldList} FROM [{tableName}] {whereClause}";
            return cmd;
        }
        #endregion
        public static async Task<List<ExpandoObject>> ExecuteQueryAsync(
                   SqlCommand cmd,
                   CancellationToken cancellationToken = default)
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
     SqlConnection conn,
     string tableNameWithAlias,
     Dictionary<string, object> whereEquals = null,
     Dictionary<string, string> whereLikes = null,
     Dictionary<string, IEnumerable<object>> whereInList = null,
     List<(string Sql, object[] Params)> whereCustom = null,
     List<(string Field, DateTime From, DateTime To)> dateRangeList = null,
     List<(string Sql, object[] Params)> joinsList = null,
     CancellationToken cancellationToken = default)
        {
            using var cmd = conn.CreateCommand();
            var whereClauses = new List<string>();

            // Tách tên bảng và alias
            var parts = tableNameWithAlias.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string tableName = parts[0];
            string alias = parts.Length > 1 ? parts[1] : null;

            // Helper thêm alias vào cột nếu cần
            string ColWithAlias(string col) => col.Contains(".") ? col : (alias != null ? $"{alias}.[{col}]" : $"[{col}]");

            // WHERE =
            if (whereEquals != null)
            {
                foreach (var kv in whereEquals)
                {
                    string param = $"@eq_{kv.Key.Split('.').Last()}";
                    whereClauses.Add($"{kv.Key} = {param}");
                    cmd.Parameters.AddWithValue(param, kv.Value ?? DBNull.Value);
                }
            }

            // WHERE LIKE
            if (whereLikes != null)
            {
                foreach (var kv in whereLikes)
                {
                    string param = $"@like_{kv.Key.Split('.').Last()}";
                    whereClauses.Add($"{kv.Key} LIKE {param}");
                    cmd.Parameters.AddWithValue(param, $"%{kv.Value}%");
                }
            }

            // WHERE IN
            if (whereInList != null)
            {
                foreach (var kv in whereInList)
                {
                    var paramNames = new List<string>();
                    int idx = 0;
                    foreach (var val in kv.Value)
                    {
                        string p = $"@in_{kv.Key.Split('.').Last()}_{idx++}";
                        paramNames.Add(p);
                        cmd.Parameters.AddWithValue(p, val ?? DBNull.Value);
                    }
                    if (paramNames.Count > 0)
                        whereClauses.Add($"{kv.Key} IN ({string.Join(", ", paramNames)})");
                }
            }

            // WHERE CUSTOM
            if (whereCustom != null)
            {
                int customIndex = 0;
                foreach (var (sql, paramValues) in whereCustom)
                {
                    var partsSql = sql.Split('?');
                    string sqlWithParams = "";
                    for (int i = 0; i < paramValues.Length; i++)
                    {
                        string p = $"@custom_{customIndex++}";
                        cmd.Parameters.AddWithValue(p, paramValues[i] ?? DBNull.Value);
                        sqlWithParams += partsSql[i] + p;
                    }
                    if (partsSql.Length > paramValues.Length)
                        sqlWithParams += partsSql.Last();
                    whereClauses.Add(sqlWithParams);
                }
            }

            // WHERE DATE RANGE
            if (dateRangeList != null)
            {
                for (int i = 0; i < dateRangeList.Count; i++)
                {
                    var r = dateRangeList[i];
                    string fromParam = $"@from_{r.Field.Split('.').Last()}_{i}";
                    string toParam = $"@to_{r.Field.Split('.').Last()}_{i}";
                    whereClauses.Add($"{r.Field} BETWEEN {fromParam} AND {toParam}");
                    cmd.Parameters.AddWithValue(fromParam, r.From);
                    cmd.Parameters.AddWithValue(toParam, r.To);
                }
            }

            // JOIN
            string joinSql = "";
            if (joinsList != null && joinsList.Count > 0)
            {
                int joinIndex = 0;
                var joinClauses = new List<string>();
                foreach (var (sql, vals) in joinsList)
                {
                    if (vals != null && vals.Length > 0)
                    {
                        var partsSql = sql.Split('?');
                        string sqlWithParams = "";
                        for (int i = 0; i < vals.Length; i++)
                        {
                            string p = $"@join_{joinIndex++}";
                            cmd.Parameters.AddWithValue(p, vals[i] ?? DBNull.Value);
                            sqlWithParams += partsSql[i] + p;
                        }
                        if (partsSql.Length > vals.Length)
                            sqlWithParams += partsSql.Last();
                        joinClauses.Add(sqlWithParams);
                    }
                    else
                    {
                        joinClauses.Add(sql);
                    }
                }
                joinSql = string.Join(" ", joinClauses);
            }

            string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            cmd.CommandText = $"SELECT COUNT(*) FROM {tableNameWithAlias} {joinSql} {whereSql}";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }
        public static string GenerateFileNumber(
            string connectionString,
            string tableName,
            string columnName,
            int storageId,
            string prefix,
            int numberLength)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            // Tạo phần prefix có kèm theo yyMM
            string fullPrefix = prefix;

            // Truy vấn lấy mã lớn nhất cùng tháng và cùng storage
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix + '%'
                AND [storage_id] = @storageId
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@prefix", fullPrefix);
            cmd.Parameters.AddWithValue("@storageId", storageId);

            var scalar = cmd.ExecuteScalar();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? "";
                var numberPart = maxCode.Length > fullPrefix.Length
                    ? maxCode.Substring(fullPrefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = fullPrefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                // Bắt đầu lại từ 1 nếu chưa có chứng từ trong tháng
                nextCode = fullPrefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static async Task<string> GenerateFileNumberEfAsync(
            DbConnection conn,
            DbTransaction? tran,
            string tableName,
            string columnName,
            int storageId,
            string prefix,
            int numberLength)
        {
            // Sinh phần ngày tháng theo định dạng yyMM
            string fullPrefix = prefix;

            // SQL truy vấn mã lớn nhất trong tháng hiện tại
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix + '%'
                AND [storage_id] = @storageId
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tran != null)
                cmd.Transaction = tran;

            var paramPrefix = cmd.CreateParameter();
            paramPrefix.ParameterName = "@prefix";
            paramPrefix.Value = fullPrefix;
            cmd.Parameters.Add(paramPrefix);

            var paramStorage = cmd.CreateParameter();
            paramStorage.ParameterName = "@storageId";
            paramStorage.Value = storageId;
            cmd.Parameters.Add(paramStorage);

            var scalar = await cmd.ExecuteScalarAsync();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? string.Empty;
                var numberPart = maxCode.Length > fullPrefix.Length
                    ? maxCode.Substring(fullPrefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = fullPrefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                nextCode = fullPrefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static string GenerateCode(
           string connectionString,
           string tableName,
           string columnName,
           int storageId,
           string prefix,
           int numberLength)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            string fullPrefix = prefix;

            // Truy vấn lấy mã lớn nhất cùng tháng và cùng storage
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix + '%'
                AND [storage_id] = @storageId
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@prefix", fullPrefix);
            cmd.Parameters.AddWithValue("@storageId", storageId);

            var scalar = cmd.ExecuteScalar();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? "";
                var numberPart = maxCode.Length > fullPrefix.Length
                    ? maxCode.Substring(fullPrefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = fullPrefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                // Bắt đầu lại từ 1 nếu chưa có chứng từ trong tháng
                nextCode = fullPrefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static async Task<string> GenerateCodeEfAsync(
            DbConnection conn,
            DbTransaction? tran,
            string tableName,
            string columnName,
            int storageId,
            string prefix,
            int numberLength)
        {
            string fullPrefix = prefix;

            // SQL truy vấn mã lớn nhất trong tháng hiện tại
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix + '%'
                AND [storage_id] = @storageId
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tran != null)
                cmd.Transaction = tran;

            var paramPrefix = cmd.CreateParameter();
            paramPrefix.ParameterName = "@prefix";
            paramPrefix.Value = fullPrefix;
            cmd.Parameters.Add(paramPrefix);

            var paramStorage = cmd.CreateParameter();
            paramStorage.ParameterName = "@storageId";
            paramStorage.Value = storageId;
            cmd.Parameters.Add(paramStorage);

            var scalar = await cmd.ExecuteScalarAsync();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? string.Empty;
                var numberPart = maxCode.Length > fullPrefix.Length
                    ? maxCode.Substring(fullPrefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = fullPrefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                nextCode = fullPrefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static string GenerateSoChungTu(
        string connectionString,
        string tableName,
        string columnName,
        int storageId,
        string prefix,
        int numberLength)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            // Escape tên bảng và cột an toàn
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix
                  AND [storage_id] = @storageId
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@prefix", prefix + "%");
            cmd.Parameters.AddWithValue("@storageId", storageId);

            var scalar = cmd.ExecuteScalar();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? "";
                var numberPart = maxCode.Length > prefix.Length
                    ? maxCode.Substring(prefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = prefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                nextCode = prefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static async Task<string> GenerateSoChungTuEfAsync(
        DbConnection conn,
        DbTransaction? tran,
        string tableName,
        string columnName,
        int storageId,
        string prefix,
        int numberLength)
        {
            // Escape tên bảng và cột để tránh SQL keyword
            string sql = $@"
                SELECT MAX([{columnName}])
                FROM [{tableName}] WITH (UPDLOCK, HOLDLOCK)
                WHERE [{columnName}] LIKE @prefix
                  AND [storage_id] = @storageId
            ";

            using var cmd = conn.CreateCommand();
            if (tran != null)
                cmd.Transaction = tran;

            cmd.CommandText = sql;

            // prefix param
            var paramPrefix = cmd.CreateParameter();
            paramPrefix.ParameterName = "@prefix";
            paramPrefix.Value = prefix + "%";
            cmd.Parameters.Add(paramPrefix);

            // storageId param
            var paramStorage = cmd.CreateParameter();
            paramStorage.ParameterName = "@storageId";
            paramStorage.Value = storageId;
            cmd.Parameters.Add(paramStorage);

            var scalar = await cmd.ExecuteScalarAsync();

            string nextCode;
            if (scalar != null && scalar != DBNull.Value)
            {
                var maxCode = scalar.ToString() ?? string.Empty;
                var numberPart = maxCode.Length > prefix.Length
                    ? maxCode.Substring(prefix.Length)
                    : "0";

                int number = int.TryParse(numberPart, out var n) ? n : 0;
                nextCode = prefix + (number + 1).ToString().PadLeft(numberLength, '0');
            }
            else
            {
                nextCode = prefix + "1".PadLeft(numberLength, '0');
            }

            return nextCode;
        }
        public static async Task<List<object>> ExecuteQuerySqlAsync(
            string connectionString,
            string sql,
            CancellationToken cancellationToken = default)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var result = new List<object>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
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

                result.Add((object)row);  // cast về object
            }

            return result;
        }
        public static async Task<decimal> ExecuteQuerySqlSumAsync(
            string connectionString,
            string sql,
            CancellationToken cancellationToken = default)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            return result == null || result == DBNull.Value
                ? 0m
                : Convert.ToDecimal(result);
        }
        public static async Task<int> ExecuteQuerySqlCountAsync(
            string connectionString,
            string sql,
            CancellationToken cancellationToken = default)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToInt32(result);
        }
    }
}
