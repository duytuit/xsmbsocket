using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace xsmbsocket.Shares.Connects
{
    public class AdoXoSoDB : IDisposable
    {
        private SqlConnection con;
        private SqlTransaction _tran; // transaction hiện tại
        public AdoXoSoDB(IOptions<ConnectionStrings> opt)
        {
            con = new SqlConnection(opt.Value.DefaultConnection);
            if (con.State == ConnectionState.Closed)
                con.Open();
        }
        public int UpsertFromObject<T>(string tableName, T obj, string keyProperty = "Id", bool returnInsertedId = false)
        {
            var type = typeof(T);
            var props = type.GetProperties();

            var keyProp = props.FirstOrDefault(p => p.Name.Equals(keyProperty, StringComparison.OrdinalIgnoreCase));
            if (keyProp == null)
                throw new ArgumentException("Không tìm thấy khóa chính: " + keyProperty);

            var keyValue = keyProp.GetValue(obj);

            var columns = new List<string>();
            var values = new List<string>();
            var insertParameters = new List<SqlParameter>();
            var updateParameters = new List<SqlParameter>();
            var updateCols = new List<string>();

            foreach (var prop in props)
            {
                var name = prop.Name;
                var value = prop.GetValue(obj) ?? DBNull.Value;

                // UPDATE: thêm tất cả trừ key
                if (!name.Equals(keyProperty, StringComparison.OrdinalIgnoreCase))
                {
                    updateCols.Add($"{name} = @{name}");
                    updateParameters.Add(new SqlParameter("@" + name, value));
                }

                // INSERT: bỏ key (Id tự tăng)
                if (!name.Equals(keyProperty, StringComparison.OrdinalIgnoreCase))
                {
                    columns.Add(name);
                    values.Add("@" + name);
                    insertParameters.Add(new SqlParameter("@" + name, value));
                }
            }

            // Thêm parameter khóa chính cho WHERE
            updateParameters.Add(new SqlParameter("@" + keyProperty, keyValue));

            string checkSql = $"SELECT COUNT(1) FROM {tableName} WHERE {keyProperty} = @{keyProperty}";
            using (var checkCmd = new SqlCommand(checkSql, con, _tran))
            {
                checkCmd.Parameters.Add(new SqlParameter("@" + keyProperty, keyValue));

                if (con.State != ConnectionState.Open)
                    con.Open();

                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    // UPDATE
                    string updateSql = $"UPDATE {tableName} SET {string.Join(", ", updateCols)} WHERE {keyProperty} = @{keyProperty}";
                    using (var updateCmd = new SqlCommand(updateSql, con, _tran))
                    {
                        updateCmd.Parameters.AddRange(updateParameters.ToArray());
                        return updateCmd.ExecuteNonQuery(); // số dòng bị ảnh hưởng
                    }
                }
                else
                {
                    // INSERT
                    string insertSql = $"INSERT INTO {tableName} ({string.Join(",", columns)}) VALUES ({string.Join(",", values)})";

                    // Nếu cần lấy Id mới chèn
                    if (returnInsertedId)
                        insertSql += "; SELECT CAST(SCOPE_IDENTITY() AS INT)";

                    using (var insertCmd = new SqlCommand(insertSql, con, _tran))
                    {
                        insertCmd.Parameters.AddRange(insertParameters.ToArray());

                        if (returnInsertedId)
                        {
                            object result = insertCmd.ExecuteScalar();
                            return result != null ? Convert.ToInt32(result) : 0;
                        }
                        else
                        {
                            return insertCmd.ExecuteNonQuery(); // số dòng insert
                        }
                    }
                }
            }
        }

        public int UpsertFromObjectByColumn<T>(
         string tableName,
         T obj,
         string[] keyColumns,
         bool returnInsertedId = false)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tên bảng không được để trống.", nameof(tableName));
            if (keyColumns == null || keyColumns.Length == 0)
                throw new ArgumentException("Phải truyền ít nhất một cột khóa.", nameof(keyColumns));

            var type = typeof(T);
            var props = type.GetProperties();

            var columns = new List<string>();
            var values = new List<string>();
            var insertParameters = new List<SqlParameter>();
            var updateParameters = new List<SqlParameter>();
            var updateCols = new List<string>();

            foreach (var prop in props)
            {
                var name = prop.Name;
                var value = prop.GetValue(obj) ?? DBNull.Value;
                bool isKey = keyColumns.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));

                // Bỏ qua cột IDENTITY khi insert
                if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
                    continue;

                // UPDATE: tất cả trừ cột khóa
                if (!isKey)
                {
                    updateCols.Add($"{name} = @{name}");
                    updateParameters.Add(new SqlParameter("@" + name, value));
                }

                // INSERT: thêm tất cả trừ ID
                columns.Add(name);
                values.Add("@" + name);
                insertParameters.Add(new SqlParameter("@" + name, value));
            }

            // WHERE condition từ keyColumns
            var whereConditions = keyColumns.Select(k => $"{k} = @{k}").ToArray();

            string sql = $@"
        IF EXISTS (SELECT 1 FROM {tableName} WHERE {string.Join(" AND ", whereConditions)})
            UPDATE {tableName} SET {string.Join(", ", updateCols)} WHERE {string.Join(" AND ", whereConditions)}
        ELSE
            INSERT INTO {tableName} ({string.Join(",", columns)}) VALUES ({string.Join(",", values)})
        ";

            if (returnInsertedId)
                sql += "; SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                // Add tất cả params (bao gồm cả keyColumns)
                cmd.Parameters.AddRange(insertParameters.ToArray());

                if (returnInsertedId)
                {
                    object result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
                else
                {
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable LoadTable(string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, _tran))
            {
                cmd.CommandType = CommandType.Text;
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        public DataTable LoadTable(string sql, string[] name, object[] value, int thamso)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, _tran))
            {
                for (int i = 0; i < thamso; i++)
                {
                    cmd.Parameters.AddWithValue(name[i], value[i]);
                }

                cmd.CommandType = CommandType.Text;
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        public int UpdateTable(string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, _tran))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        public int UpdateTable(string sql, string[] name, object[] value, int thamso)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, _tran))
            {
                for (int i = 0; i < thamso; i++)
                {
                    cmd.Parameters.AddWithValue(name[i], value[i]);
                }

                return cmd.ExecuteNonQuery();
            }
        }

        public int DeleteById(string tableName, object idValue, string keyProperty = "Id")
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tên bảng không được để trống.", nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyProperty))
                throw new ArgumentException("Tên cột khóa chính không được để trống.", nameof(keyProperty));

            string sql = $"DELETE FROM {tableName} WHERE {keyProperty} = @{keyProperty}";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                cmd.Parameters.AddWithValue("@" + keyProperty, idValue ?? DBNull.Value);
                return cmd.ExecuteNonQuery(); // Trả về số dòng bị xóa
            }
        }
        public int DeleteWhere(
        string tableName,
        Dictionary<string, object> whereEquals = null,
        Dictionary<string, IEnumerable<object>> whereInList = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tên bảng không được để trống.", nameof(tableName));

            var conditions = new List<string>();
            var parameters = new List<SqlParameter>();

            // --- WHERE column = @param ---
            if (whereEquals != null)
            {
                foreach (var kvp in whereEquals)
                {
                    string paramName = $"@{kvp.Key}";
                    conditions.Add($"{kvp.Key} = {paramName}");
                    parameters.Add(new SqlParameter(paramName, kvp.Value ?? DBNull.Value));
                }
            }

            // --- WHERE column IN (@p0, @p1, ...) ---
            if (whereInList != null)
            {
                foreach (var kvp in whereInList)
                {
                    var values = kvp.Value?.ToList() ?? new List<object>();
                    if (values.Count == 0) continue;

                    var inParams = new List<string>();
                    for (int i = 0; i < values.Count; i++)
                    {
                        string paramName = $"@{kvp.Key}_in{i}";
                        inParams.Add(paramName);
                        parameters.Add(new SqlParameter(paramName, values[i] ?? DBNull.Value));
                    }

                    conditions.Add($"{kvp.Key} IN ({string.Join(",", inParams)})");
                }
            }

            if (conditions.Count == 0)
                throw new ArgumentException("Cần ít nhất một điều kiện WHERE để tránh xóa toàn bộ bảng.");

            string sql = $"DELETE FROM {tableName} WHERE {string.Join(" AND ", conditions)}";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                return cmd.ExecuteNonQuery();
            }
        }
        public DataRow GetSingleRecord(string tableName, object value, string keyProperty = "Id", bool descending = false)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tên bảng không được để trống.", nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyProperty))
                throw new ArgumentException("Tên cột khóa chính không được để trống.", nameof(keyProperty));

            string orderDir = descending ? "DESC" : "ASC";
            string sql = $"SELECT TOP 1 * FROM {tableName}";

            if (value != null)
                sql += $" WHERE {keyProperty} = @val";

            sql += $" ORDER BY {keyProperty} {orderDir}";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                if (value != null)
                    cmd.Parameters.AddWithValue("@val", value);

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
        }
        public string GenerateSoChungTu(string tableName, string columnName, string prefix, int numberLength)
        {
            string sql = $@"
        SELECT MAX({columnName}) 
        FROM {tableName} 
        WHERE {columnName} LIKE '{prefix}%'
    ";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                object result = cmd.ExecuteScalar();
                if (result != DBNull.Value && result != null)
                {
                    string maxCode = result.ToString();
                    // Cắt phần số từ mã
                    string numberPart = maxCode.Substring(prefix.Length);
                    if (int.TryParse(numberPart, out int number))
                    {
                        number++;
                        return prefix + number.ToString().PadLeft(numberLength, '0');
                    }
                }
            }

            // Nếu chưa có dữ liệu → trả về mã đầu tiên
            return prefix + 1.ToString().PadLeft(numberLength, '0');
        }
        public double GetSumValue(string tableName, string sumColumn, string whereColumn, object whereValue)
        {
            if (string.IsNullOrWhiteSpace(tableName) ||
                string.IsNullOrWhiteSpace(sumColumn) ||
                string.IsNullOrWhiteSpace(whereColumn))
                throw new ArgumentException("Tên bảng, tên cột tổng và cột điều kiện không được để trống.");

            string sql = $@"
        SELECT SUM({sumColumn}) 
        FROM {tableName} 
        WHERE {whereColumn} = @WhereValue";

            using (var cmd = new SqlCommand(sql, con, _tran))
            {
                cmd.Parameters.AddWithValue("@WhereValue", whereValue ?? DBNull.Value);
                object result = cmd.ExecuteScalar();
                if (result != DBNull.Value && result != null)
                    return Convert.ToDouble(result);
            }
            return 0;
        }
        #region Transaction Methods
        public void BeginTransaction()
        {
            if (_tran == null)
            {
                if (con.State != ConnectionState.Open)
                    con.Open();
                _tran = con.BeginTransaction();
            }
        }

        public void CommitTransaction()
        {
            _tran?.Commit();
            _tran = null;
        }

        public void RollbackTransaction()
        {
            _tran?.Rollback();
            _tran = null;
        }
        #endregion
        // 👇 QUAN TRỌNG: Giải phóng kết nối SQL
        public void Dispose()
        {
            if (_tran != null)
            {
                _tran.Dispose();
                _tran = null;
            }

            if (con != null)
            {
                if (con.State != ConnectionState.Closed)
                    con.Close();

                con.Dispose();
                con = null;
            }
        }
    }
}
