using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public class XoSoTrucTiepHtmlParser
    {
        private static readonly Regex DateRegex = new Regex(
            @"\b(\d{1,2}/\d{1,2}/\d{4})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PrizeRegex = new Regex(
            @"^giai\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public List<XoSoResult> Parse(string html)
        {
            var results = new List<XoSoResult>();

            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            var document = new HtmlParser().ParseDocument(html);

            foreach (var section in document.QuerySelectorAll(".khung"))
            {
                var drawDate = ParseDrawDate(section);
                if (drawDate != DateTime.MinValue)
                {
                    ParseSection(section, drawDate, results);
                }
            }

            return results;
        }

        private static void ParseSection(
            IElement section,
            DateTime drawDate,
            List<XoSoResult> results)
        {
            var title = section.QuerySelector(".tieude")?.TextContent ?? string.Empty;
            var region = ParseRegion(title);
            var content = section.QuerySelector(".noidung");

            if (content == null)
            {
                return;
            }

            var titleRow = content.QuerySelector(".mtmn_title, .mb_title");
            if (titleRow == null)
            {
                return;
            }

            var titleCells = titleRow.Children.ToList();
            var names = titleCells
                .Skip(1)
                .Select(cell => Normalize(cell.TextContent))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (names.Count == 0)
            {
                return;
            }

            var codeRow = content.QuerySelector(".mtmn_title + .mtmn_title");
            var codes = codeRow == null
                ? new List<string>()
                : codeRow.Children
                    .Skip(1)
                    .Select(cell => Normalize(cell.TextContent))
                    .ToList();

            var prizeRows = content
                .QuerySelectorAll(".mtmn_content, .mb_content")
                .ToList();

            for (var provinceIndex = 0;
                provinceIndex < names.Count;
                provinceIndex++)
            {
                var provinceName = names[provinceIndex];
                var prizes = new List<XoSoPrize>();
                foreach (var prizeRow in prizeRows)
                {
                    var cells = prizeRow.Children.ToList();
                    var cellIndex = provinceIndex + 1;
                    if (cells.Count <= cellIndex)
                    {
                        continue;
                    }

                    var prizeName = Normalize(cells[0].TextContent);
                    if (!PrizeRegex.IsMatch(NormalizeForSearch(prizeName)) &&
                        !NormalizeForSearch(prizeName).StartsWith("giai "))
                    {
                        continue;
                    }

                    var numbers = ExtractNumbers(cells[cellIndex]);
                    if (numbers.Count == 0)
                    {
                        continue;
                    }

                    prizes.Add(new XoSoPrize
                    {
                        PrizeName = prizeName,
                        Numbers = numbers
                    });
                }

                if (prizes.Count == 0)
                {
                    continue;
                }

                var provinceCode = provinceIndex < codes.Count &&
                    !string.IsNullOrWhiteSpace(codes[provinceIndex])
                    ? codes[provinceIndex]
                    : provinceName;

                results.Add(new XoSoResult
                {
                    Region = region,
                    ProvinceCode = region == "Miền Bắc" ? "Xổ Số Miền Bắc" : provinceCode,
                    ProvinceName = region == "Miền Bắc" ? "Miền Bắc" : provinceName,
                    DrawDate = drawDate,
                    Prizes = prizes
                });
            }
        }

        private static DateTime ParseDrawDate(IElement section)
        {
            var titleLoto = section.QuerySelector("[id^='titleLoto']");
            var match = DateRegex.Match(titleLoto?.TextContent ?? string.Empty);
            if (!match.Success)
            {
                var dayMonth = Normalize(section.QuerySelector("._date .dayMonth")?.TextContent);
                var year = Normalize(section.QuerySelector("._date .year")?.TextContent);
                match = DateRegex.Match($"{dayMonth}/{year}");
            }
            return match.Success && DateTime.TryParseExact(
                match.Groups[1].Value,
                new[] { "d/M/yyyy", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var drawDate)
                ? drawDate
                : DateTime.MinValue;
        }

        private static string ParseRegion(string value)
        {
            var text = NormalizeForSearch(value);
            var match = Regex.Match(
                text,
                @"xo so mien (bac|trung|nam)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return match.Groups[1].Value.ToLowerInvariant() switch
            {
                "bac" => "Miền Bắc",
                "trung" => "Miền Trung",
                "nam" => "Miền Nam",
                _ => "Không xác định"
            };
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        }

        private static List<string> ExtractNumbers(IElement cell)
        {
            var value = cell.InnerHtml ?? string.Empty;
            value = Regex.Replace(
                value,
                @"<br\s*/?>",
                "\n",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            value = Regex.Replace(value, @"<[^>]+>", " ");
            value = WebUtility.HtmlDecode(value);

            return Regex.Matches(value, @"\d+")
                .Cast<Match>()
                .Select(match => match.Value)
                .ToList();
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
