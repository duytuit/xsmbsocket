using System;
using System.Globalization;
using System.Linq;
using System.Text;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public static class LotteryResultIdentity
    {
        private static readonly string[][] Aliases = {
            new[] { "HCM", "TP HCM", "TPHCM", "TP.HCM", "TP. HCM", "Ho Chi Minh", "Thành phố Hồ Chí Minh" },
            new[] { "DL", "Đắk Lắk", "Dac Lac", "Đắc Lắc", "Daklak", "DACLAC", "DLK" },
            new[] { "DNO", "Đắk Nông", "Dac Nong", "Đắc Nông", "Daknong", "DACNONG", "DNONG" },
            new[] { "DNA", "Đà Nẵng", "DNG" },
            new[] { "QNG", "Quảng Ngãi", "QNI" }
        };

        public static string Key(string value)
        {
            var text = (value ?? "").Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            text = new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).ToArray());
            return text.StartsWith("xoso") ? text.Substring(4) : text;
        }

        public static void Normalize(XoSoResult result)
        {
            var station = Aliases.FirstOrDefault(a => a.Any(x => Key(x) == Key(result.ProvinceCode)))
                ?? Aliases.FirstOrDefault(a => a.Any(x => Key(x) == Key(result.ProvinceName)));
            if (station != null) { result.ProvinceCode = station[0]; result.ProvinceName = station[1]; }
            result.Region = Key(result.Region) switch {
                "mb" or "mienbac" => "Miền Bắc", "mn" or "miennam" => "Miền Nam",
                "mt" or "mientrung" => "Miền Trung", _ => result.Region?.Trim()
            };
            if (station != null) result.Region = station[0] == "HCM" ? "Miền Nam" : "Miền Trung";
            result.ProvinceCode = result.ProvinceCode?.Trim();
            result.ProvinceName = result.ProvinceName?.Trim();
            result.DrawDate = result.DrawDate.Date;
            if (string.IsNullOrWhiteSpace(result.Region) || string.IsNullOrWhiteSpace(result.ProvinceCode) || string.IsNullOrWhiteSpace(result.ProvinceName))
                throw new ArgumentException("Region, province code and province name are required.");
        }

        public static bool Matches(XoSoResult left, XoSoResult right)
        {
            Normalize(left); Normalize(right);
            return left.DrawDate == right.DrawDate && Key(left.Region) == Key(right.Region)
                && (Key(left.ProvinceCode) == Key(right.ProvinceCode) || Key(left.ProvinceName) == Key(right.ProvinceName));
        }
    }
}
