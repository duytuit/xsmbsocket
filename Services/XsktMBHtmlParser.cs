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
    public class XsktMBHtmlParser
    {
        private static readonly Regex DateRegex = new Regex(
            @"\b(\d{1,2}[-/]\d{1,2}[-/]\d{4})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PrizeRegex = new Regex(
            @"^(db|giai\s+[1-7])$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
                var table = box.QuerySelector("table.kqmb");
                if (drawDate == DateTime.MinValue ||
                    (expectedDate.HasValue && drawDate.Date != expectedDate.Value) ||
                    table == null)
                {
                    continue;
                }

                var prizes = ParsePrizes(table);
                if (prizes.Count == 0)
                {
                    continue;
                }

                results.Add(new XoSoResult
                {
                    Region = "Miền Bắc",
                    ProvinceCode = "Xổ Số Miền Bắc",
                    ProvinceName = "Miền Bắc",
                    DrawDate = drawDate,
                    Prizes = prizes
                });
            }

            return results;
        }

        private static List<XoSoPrize> ParsePrizes(IElement table)
        {
            var prizes = new List<XoSoPrize>();
            foreach (var row in table.QuerySelectorAll("tr"))
            {
                var label = NormalizeForSearch(row.QuerySelector(".txt-giai")?.TextContent);
                if (!PrizeRegex.IsMatch(label))
                {
                    continue;
                }

                var numberCell = row.QuerySelector("td.number");
                var numbers = numberCell?.QuerySelectorAll("span")
                    .Select(span => Normalize(span.TextContent))
                    .Where(number => Regex.IsMatch(number, @"^\d+$"))
                    .ToList();

                if (numbers == null || numbers.Count == 0)
                {
                    numbers = Regex.Matches(numberCell?.TextContent ?? string.Empty, @"\d+")
                        .Cast<Match>()
                        .Select(match => match.Value)
                        .ToList();
                }

                if (numbers.Count == 0)
                {
                    continue;
                }

                prizes.Add(new XoSoPrize
                {
                    PrizeName = string.Equals(label, "db", StringComparison.OrdinalIgnoreCase)
                        ? "ĐB"
                        : label,
                    Numbers = numbers
                });
            }

            return prizes;
        }

        private static DateTime ParseDate(string value)
        {
            var match = DateRegex.Match(value ?? string.Empty);
            return match.Success && DateTime.TryParseExact(
                match.Groups[1].Value,
                new[] { "d-M-yyyy", "dd-MM-yyyy", "d/M/yyyy", "dd/MM/yyyy" },
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
    }
}