using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace xsmbsocket.Models
{
    public class XoSoPrize
    {
        private string prizeName;

        public string PrizeName
        {
            get => prizeName;
            set => prizeName = NormalizePrizeName(value);
        }

        private static string NormalizePrizeName(string value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant()
                .Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var key = new string(text.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(c)).ToArray());

            if (key.StartsWith("giai")) key = key.Substring(4);
            if (key == "db" || key == "dacbiet") return "ĐB";
            if (key.StartsWith("g")) key = key.Substring(1);

            key = key switch
            {
                "nhat" => "1", "nhi" => "2", "ba" => "3", "tu" => "4",
                "nam" => "5", "sau" => "6", "bay" => "7", "tam" => "8",
                _ => key
            };

            return key.Length == 1 && key[0] >= '1' && key[0] <= '8'
                ? "G" + key
                : value;
        }

        public List<string> Numbers { get; set; }
            = new List<string>();
    }
}
