

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query.Internal;
using xsmbsocket.Shares.Connects;

namespace xsmbsocket.Shares
{
    public static class Helper
    {

        private static string _botToken;
        private static readonly HttpClient _http = new HttpClient();

        // Gọi lúc Startup để nạp token từ appsettings
        public static void ConfigureTelegram(TelegramSettings settings)
        {
            _botToken = settings.BotToken;
        }

        // Gửi tin nhắn Telegram
        public static async Task<bool> SendTelegramMessageAsync(string message)
        {
            if (string.IsNullOrEmpty(_botToken))
                throw new Exception("Bot token chưa được cấu hình từ appsettings.");

            string text = HttpUtility.UrlEncode(message);

            string url = $"{_botToken}{text}";

            var response = await _http.GetAsync(url);

            return response.IsSuccessStatusCode;
        }
        public static List<Dictionary<string, object>> ConfigFormType(int type)
        {
            var result = new List<Dictionary<string, object>>();
            switch (type)
            {
                case 1:
                    result = new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["from_dept"] = 0,
                                        ["to_dept"] = new List<int> { 5,7,4,3,2,6 },
                                        ["confirm_by_type"] = "",
                                        ["confirm_from_dept"] = 0,
                                        ["confirm_to_dept"] = 2,
                                        ["confirm_by_from_dept"] = new List<int> { 3 },
                                        ["confirm_by_to_dept"] = new List<int> { 4, 5 },
                                        ["user_cat"] = new List<string> { "240929" },
                                        ["user_dap"] = new List<string> { "130764" },
                                        ["user_cam"] = new List<string> { "130206"},
                                        ["user_buredo"] = new List<string> { "140511"},
                                        ["user_laprap"] = new List<string> { "10281" },
                                        ["user_kiemtra"] = new List<string> { "131078"},
                                        ["data_table"] = new Dictionary<string, string>
                                        {
                                            ["code"] = "",
                                            ["quantity"] = "",
                                            ["size"] = "",
                                            ["unit_price"] = "",
                                            ["location_c"] = "",
                                            ["usage_status"] = ""
                                        }
                                    }
                                };
                    break;

                case 2:
                    result = new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["id"] = 2,
                                        ["from_dept"] = new List<int> { 9, 10 },
                                        ["to_dept"] = new List<int> { 6 },
                                        ["confirm_by_type"] = new List<int> { 1 },
                                        ["confirm_from_dept"] = 1,
                                        ["confirm_to_dept"] = 1,
                                        ["confirm_by_from_dept"] = new List<int> { 2 },
                                        ["confirm_by_to_dept"] = new List<int> { 3 },
                                        ["data_table"] = new Dictionary<string, string>
                                        {
                                            ["code"] = "",
                                            ["quantity"] = "",
                                            ["note"] = ""
                                        }
                                    }
                                };
                    break;

                default:
                    result = new List<Dictionary<string, object>>();
                    break;
            }
            return result;
        }
        public static object ConfigRequiredByType(int type)
        {
            var result = new object();
            switch (type)
            {
                case 1:
                    result = new
                    {
                        from_dept = 0,
                        to_dept = new List<int> { 5, 7, 4, 3, 2, 6 },
                        confirm_by_type = "",
                        confirm_from_dept = 0,
                        confirm_to_dept = 2,
                        confirm_by_from_dept = new List<int> { 3 },
                        confirm_by_to_dept = new List<int> { 4, 5 },
                        emp_dept = new[]
                                    {
                                        new {
                                            id_dept = 5,
                                            code_emp = new List<int> { 240929, 240923 }
                                        },
                                        new {
                                            id_dept = 7,
                                            code_emp = new List<int> { 240930, 240931 }
                                        },
                                        new {
                                            id_dept = 4,
                                            code_emp = new List<int> { 240929, 240923 }
                                        },
                                        new {
                                            id_dept = 3,
                                            code_emp = new List<int> { 240930, 240931 }
                                        },
                                        new {
                                            id_dept = 2,
                                            code_emp = new List<int> { 240929, 240923 }
                                        },
                                        new {
                                            id_dept = 6,
                                            code_emp = new List<int> { 240930, 240931 }
                                        }
                                    }
                    };
                    break;
                case 2:
                    result = new
                    {
                        from_dept = 0,
                        to_dept = new List<int> { 5, 7, 4, 3, 2, 6 },
                        confirm_by_type = "",
                        confirm_from_dept = 0,
                        confirm_to_dept = 2,
                        confirm_by_from_dept = new List<int> { 3 },
                        confirm_by_to_dept = new List<int> { 4, 5 },
                        user_cat = new List<string> { "240929" },
                        user_dap = new List<string> { "130764" },
                        user_cam = new List<string> { "130206" },
                        user_buredo = new List<string> { "140511" },
                        user_laprap = new List<string> { "10281" },
                        user_kiemtra = new List<string> { "131078" },
                        data_table = new
                        {
                            code = "",
                            quantity = "",
                            size = "",
                            unit_price = "",
                            location_c = "",
                            usage_status = ""
                        }
                    };
                    break;
                default:
                    break;
            }
            return result;
        }
         public static async Task<UploadResult> ProcessFileAsync(IFormFile file,string webRootPath,string? folder)
        {
            long _fileSizeLimit = 50 * 1024 * 1024;            // 50 MB
            string[] _permittedExtensions = { ".jpg", ".png", ".pdf" ,".xls",".xlsx"};
            if (file == null || file.Length == 0)
                return new UploadResult(false, "File rỗng.");

            if (file.Length > _fileSizeLimit)
                return new UploadResult(false, "File quá lớn (max 50 MB).");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            bool found = Array.IndexOf(_permittedExtensions, ext) >= 0;
            if (!found)
                return new UploadResult(false, $"Không hỗ trợ định dạng {ext}.");

            // Thư mục : wwwroot/uploads/yyyy/MM
            // UNC gốc – dùng chuỗi verbatim @"" để đỡ phải gấp đôi \\
            string rootPath = Path.Combine(webRootPath, "uploads");

            // Nếu có folder thì thêm vào UNC path
            string targetFolder = !string.IsNullOrWhiteSpace(folder)
                ? Path.Combine(rootPath, folder.Replace("..", "").Trim())
                : rootPath;

            // tạo thư mục (nếu chưa có)
            Directory.CreateDirectory(targetFolder);
            string date_file = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-";
            var filePath = Path.Combine(targetFolder, date_file+file.FileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Đường dẫn trả về cho client
            var relativePath = !string.IsNullOrWhiteSpace(folder)
                ? $"uploads/{folder}/{date_file}{file.FileName}"
                : $"uploads/{date_file}{file.FileName}";

            return new UploadResult(true, "OK", $"{relativePath}", "https://xsmbsocket.io.vn/"+$"{relativePath}");
        }
        public static string GetClientInfo(IHttpContextAccessor accessor, string clientNameFromBody = null)
        {
            var context = accessor.HttpContext;

            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? context.Connection.RemoteIpAddress?.ToString();

            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var clientNameFromHeader = context.Request.Headers["X-Client-Name"].ToString();

            string hostName = null;
            try
            {
                var ipAddr = context.Connection.RemoteIpAddress;
                if (ipAddr != null && !IPAddress.IsLoopback(ipAddr))
                {
                    var entry = Dns.GetHostEntry(ipAddr);
                    hostName = entry.HostName;
                }
            }
            catch
            {
                hostName = "Unable to resolve";
            }

            var result = new
            {
                ipAddress = ip,
                userAgent = userAgent,
                clientName_FromHeader = clientNameFromHeader,
                clientName_FromBody = clientNameFromBody,
                hostname_Resolved = hostName
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        public static DataTable ToDataTable(IEnumerable<dynamic> items)
        {
            var dataTable = new DataTable("Data");

            if (items == null) return dataTable;

            var list = items.ToList();
            if (!list.Any()) return dataTable;

            // 🔹 ExpandoObject / Dictionary
            if (list[0] is IDictionary<string, object>)
            {
                var dict = (IDictionary<string, object>)list[0];

                foreach (var key in dict.Keys)
                    dataTable.Columns.Add(key);

                foreach (IDictionary<string, object> item in list)
                {
                    var row = dataTable.NewRow();
                    foreach (var key in dict.Keys)
                        row[key] = item[key] ?? DBNull.Value;

                    dataTable.Rows.Add(row);
                }

                return dataTable;
            }

            // 🔹 dynamic object (Dapper, anonymous)
            var props = list[0].GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                dataTable.Columns.Add(prop.Name, type);
            }

            foreach (var item in list)
            {
                var values = new object[props.Length];

                for (int i = 0; i < props.Length; i++)
                {
                    values[i] = props[i].GetValue(item) ?? DBNull.Value;
                }

                dataTable.Rows.Add(values);
            }

            return dataTable;
        }
        public static string NumberToVietnameseWords(double number)
        {
            if (number == 0) return "Không đồng";

            string[] unitNumbers = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string[] placeValues = { "", "nghìn", "triệu", "tỷ" };

            string sNumber = number.ToString("#");
            int length = sNumber.Length;
            int placeValue = 0;
            string result = "";
            string suffix = "";

            while (length > 0)
            {
                int threeDigits = (length >= 3) ? int.Parse(sNumber.Substring(length - 3, 3)) : int.Parse(sNumber.Substring(0, length));
                length -= 3;

                if (threeDigits > 0 || placeValue == 3)
                {
                    string group = ReadThreeDigits(threeDigits, unitNumbers);
                    result = group + " " + placeValues[placeValue] + " " + suffix + result;
                    suffix = "";
                }
                placeValue++;
                if (placeValue > 3) placeValue = 1;
            }

            result = result.Trim();
            result = char.ToUpper(result[0]) + result.Substring(1) + " đồng";
            return result;
        }

        public static List<object> Permissions()
        {
            return new[]
            {
                new { id = 1, role = "laixe" },
                new { id = 2, role = "vanphong" },
                new { id = 3, role = "giaonhan" },
                new { id = 6, role = "quanly" },
                new { id = 7, role = "dieuxe" }
            }.ToList<object>();
        }
        public static string ReadThreeDigits(int number, string[] unitNumbers)
        {
            int hundreds = number / 100;
            int tens = (number % 100) / 10;
            int units = number % 10;
            string result = "";

            if (hundreds > 0)
            {
                result += unitNumbers[hundreds] + " trăm";
                if (tens == 0 && units > 0) result += " linh";
            }

            if (tens > 1)
            {
                result += " " + unitNumbers[tens] + " mươi";
                if (units == 1) result += " mốt";
                else if (units == 5) result += " lăm";
                else if (units > 0) result += " " + unitNumbers[units];
            }
            else if (tens == 1)
            {
                result += " mười";
                if (units == 5) result += " lăm";
                else if (units > 0) result += " " + unitNumbers[units];
            }
            else if (tens == 0 && units > 0)
            {
                result += " " + unitNumbers[units];
            }

            return result.Trim();
        }
        public static string DriverStatus(int status)
        {
            return status switch
            {
                0 => "Chưa nhận chuyến",
                1 => "Đã nhận chuyến",
                2 => "Đã hoàn thành",
                3 => "Đổi ca",
                _ => "Không xác định",
            };
        }
    }
        public class UploadResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string Path { get; set; }
            public string FullPath { get; set; }

            public UploadResult(bool success, string message=null, string path=null, string fullPath=null)
            {
                Success = success;
                Message = message;
                Path = path;
                FullPath = fullPath;
            }
        }
}