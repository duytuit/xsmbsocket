using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public static class LotteryResultsParser
    {
        public static List<XoSoResult> Parse(string data, string type, DateTime date)
        {
            var results = new List<XoSoResult>();
            var north = type == "mienbac";
            var prefix = type switch { "mienbac" => "0|1", "miennam" => "0|2", "mientrung" => "0|3", _ => null };
            if (prefix == null || string.IsNullOrWhiteSpace(data) || !data.StartsWith(prefix + "!"))
                return results;

            var names = north
                ? new[] { "\u0110B", "G1", "G2", "G3", "G4", "G5", "G6", "G7" }
                : new[] { "G8", "G7", "G6", "G5", "G4", "G3", "G2", "G1", "\u0110B" };
            var counts = north ? new[] { 1, 1, 2, 6, 4, 6, 3, 4 } : new[] { 1, 1, 3, 1, 7, 2, 1, 1, 1 };
            var lengths = north ? new[] { 5, 5, 5, 5, 4, 4, 3, 2 } : new[] { 2, 3, 4, 4, 5, 5, 5, 5, 6 };
            foreach (var block in data.Split('!').Skip(1))
            {
                var at = block.IndexOf('@');
                if (at < 0) continue;
                var header = block.Substring(0, at).Split('|');
                var fields = block.Substring(at + 1).Split('|');
                if (header.Length != 4) continue;
                var result = new XoSoResult
                {
                    Region = north ? "Mi\u1ec1n B\u1eafc" : type == "miennam" ? "Mi\u1ec1n Nam" : "Mi\u1ec1n Trung",
                    ProvinceCode = north ? "X\u1ed5 S\u1ed1 Mi\u1ec1n B\u1eafc" : header[2].Trim(),
                    ProvinceName = north ? "Mi\u1ec1n B\u1eafc" : header[3].Trim(),
                    DrawDate = date.Date
                };
                if (string.IsNullOrWhiteSpace(result.ProvinceCode) || string.IsNullOrWhiteSpace(result.ProvinceName)) continue;
                for (var i = 0; i < names.Length && i < fields.Length; i++)
                {
                    var numbers = fields[i].Split('-');
                    if (numbers.Length != counts[i] || numbers.Any(n => !Regex.IsMatch(n, "^[0-9]{" + lengths[i] + "}$")))
                        continue;
                    result.Prizes.Add(new XoSoPrize { PrizeName = names[i], Numbers = numbers.ToList() });
                }
                // Ignore the northern ticket series after G7; preserve leading zeroes.
                if (result.Prizes.Count > 0) results.Add(result);
            }
            return results;
        }
    }
}
