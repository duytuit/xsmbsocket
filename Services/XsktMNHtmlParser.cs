using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public class XsktMNHtmlParser
    {
        private static readonly Regex DateRegex = new Regex(
            @"\b(\d{1,2}-\d{1,2}-\d{4})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> PrizeNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "g8",
                "g7",
                "g6",
                "g5",
                "g4",
                "g3",
                "g2",
                "g1",
                "db"
            };

        public List<XoSoResult> Parse(string html, DateTime? targetDate = null)
        {
            var results = new List<XoSoResult>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            var document = new HtmlParser().ParseDocument(html);
            var expectedDate = targetDate?.Date;

            foreach (var box in document.QuerySelectorAll(".box"))
            {
                var title = box.QuerySelector(".regional-title.clearfix");
                var drawDate = ParseDate(title?.QuerySelector("h2")?.TextContent);
                var table = box.QuerySelector(
                    "table.colthreecity, table.colfourcity, table[class*='city'][class*='colgiai']");
                if (drawDate == DateTime.MinValue ||
                    (expectedDate.HasValue && drawDate.Date != expectedDate.Value) ||
                    table == null)
                {
                    continue;
                }

                var provinces = ParseProvinceHeaders(table);
                var prizeRows = ParsePrizeRows(table);
                foreach (var province in provinces)
                {
                    var prizes = prizeRows
                        .Where(row => province.Index < row.NumbersByProvince.Count)
                        .Select(row => new XoSoPrize
                        {
                            PrizeName = row.Name,
                            Numbers = row.NumbersByProvince[province.Index]
                        })
                        .Where(prize => prize.Numbers.Count > 0)
                        .ToList();

                    if (prizes.Count == 0)
                    {
                        continue;
                    }

                    results.Add(new XoSoResult
                    {
                        Region = "Miền nam",
                        ProvinceCode = province.Code,
                        ProvinceName = province.Name,
                        DrawDate = drawDate,
                        Prizes = prizes
                    });
                }
            }

            return results;
        }

        private static List<ProvinceHeader> ParseProvinceHeaders(IElement table)
        {
            var headerRow = table.QuerySelector("tr");
            if (headerRow == null)
            {
                return new List<ProvinceHeader>();
            }

            return headerRow.QuerySelectorAll("th")
                .Skip(1)
                .Select((cell, index) => new ProvinceHeader
                {
                    Index = index,
                    Code = cell.GetAttribute("data-pid") ?? Normalize(cell.TextContent),
                    Name = Normalize(cell.TextContent)
                })
                .Where(province => !string.IsNullOrWhiteSpace(province.Name))
                .ToList();
        }

        private static List<PrizeRow> ParsePrizeRows(IElement table)
        {
            var rows = new List<PrizeRow>();
            foreach (var row in table.QuerySelectorAll("tr"))
            {
                var cells = row.Children.ToList();
                if (cells.Count < 2)
                {
                    continue;
                }

                var name = NormalizeForSearch(cells[0].TextContent);
                if (!PrizeNames.Contains(name))
                {
                    continue;
                }

                rows.Add(new PrizeRow
                {
                    Name = name == "db" ? "ĐB" : name.ToUpperInvariant(),
                    NumbersByProvince = cells
                        .Skip(1)
                        .Select(cell => cell.QuerySelectorAll("[data-nc]")
                            .Select(number => Normalize(number.TextContent))
                            .Where(number => Regex.IsMatch(number, @"^\d+$"))
                            .ToList())
                        .ToList()
                });
            }

            return rows;
        }

        private static DateTime ParseDate(string value)
        {
            var match = DateRegex.Match(value ?? string.Empty);
            return match.Success && DateTime.TryParseExact(
                match.Groups[1].Value,
                new[] { "d-M-yyyy", "dd-MM-yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var drawDate)
                ? drawDate
                : DateTime.MinValue;
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        }

        private static string NormalizeForSearch(string value)
        {
            var normalized = Normalize(value)
                .Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                    UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(character switch
                {
                    'đ' => 'd',
                    'Đ' => 'D',
                    _ => character
                });
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private sealed class ProvinceHeader
        {
            public int Index { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
        }

        private sealed class PrizeRow
        {
            public string Name { get; set; }
            public List<List<string>> NumbersByProvince { get; set; }
        }
    }
}