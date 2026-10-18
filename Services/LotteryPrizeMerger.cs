using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public static class LotteryPrizeMerger
    {
        public static bool FillMissing(List<XoSoPrize> existing, IEnumerable<XoSoPrize> incoming)
        {
            var changed = false;
            foreach (var prize in incoming)
            {
                if (prize?.Numbers == null || !prize.Numbers.Any(n => !string.IsNullOrWhiteSpace(n))) continue;
                var key = Normalize(prize.PrizeName);
                if (key.Length == 0) continue;
                var matches = existing.Where(p => p != null && Normalize(p.PrizeName) == key).ToList();
                if (matches.Any(p => p.Numbers != null && p.Numbers.Any(n => !string.IsNullOrWhiteSpace(n)))) continue;
                var target = matches.FirstOrDefault();
                if (target == null)
                    existing.Add(new XoSoPrize { PrizeName = prize.PrizeName, Numbers = new List<string>(prize.Numbers) });
                else
                    target.Numbers = new List<string>(prize.Numbers);
                changed = true;
            }
            return changed;
        }

        private static string Normalize(string name)
        {
            var text = (name ?? "").Trim().ToLowerInvariant().Replace('\u0111', 'd').Normalize(NormalizationForm.FormD);
            var key = new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).ToArray());
            if (key.StartsWith("giai")) key = key.Substring(4);
            if (key == "db" || key == "dacbiet") return "db";
            if (key.StartsWith("g")) key = key.Substring(1);
            return key switch { "nhat" => "1", "nhi" => "2", "ba" => "3", "tu" => "4", "nam" => "5", "sau" => "6", "bay" => "7", "tam" => "8", _ => key };
        }
    }
}
