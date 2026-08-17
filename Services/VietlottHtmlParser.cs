using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using xsmbsocket.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System;
using System.Text;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace xsmbsocket.Services
{
    public class VietlottHtmlParser
    {
        public List<VietlottResult> Parse(IDocument document)
        {
            var results = new List<VietlottResult>();
            ParseNormalGames(document, results);
            ParseBingo(document,results);
            ParseKeno(document, results);
            ParseMax3D(document,results);
            ParseMax3DPro(document,results);
            return results;
        }
        private void ParseNormalGames(IDocument document,List<VietlottResult> results)
        {
            var boxes =
                document.QuerySelectorAll(
                    ".box_kqtt");

            foreach (var box in boxes)
            {
                var image =
                    box.QuerySelector(
                        ".box_kqtt_nd_img img");

                if (image == null)
                    continue;

                string alt =
                    image.GetAttribute("alt") ?? "";

                string gameCode = null;

                if (alt.Contains(
                    "LOTTO",
                    StringComparison.OrdinalIgnoreCase))
                {
                    gameCode = "LOTTO535";
                }
                else if (alt.Contains(
                    "Mega",
                    StringComparison.OrdinalIgnoreCase))
                {
                    gameCode = "MEGA645";
                }
                else if (alt.Contains(
                    "Power",
                    StringComparison.OrdinalIgnoreCase))
                {
                    gameCode = "POWER655";
                }

                if (gameCode == null)
                    continue;

                var main =
                    box.QuerySelector(
                        ".box_kqtt_nd_chinh, .box_kqtt_nd_chinh_home");

                if (main == null)
                    continue;

                string header =
                    main.TextContent
                        .Replace("\r", " ")
                        .Replace("\n", " ");

                var drawMatch =
                    Regex.Match(
                        header,
                        @"(?:kỳ|ky)\s*#?(\d+)",
                        RegexOptions.IgnoreCase);

                var dateMatch =
                    Regex.Match(
                        header,
                        @"(\d{2}/\d{2}/\d{4})");

                if (!drawMatch.Success ||
                    !dateMatch.Success)
                    continue;

                var balls =
                    main.QuerySelectorAll(
                        ".day_so_ket_qua_v2 > .bong_tron");

                var numbers =
                    new List<string>();

                var special =
                    new List<string>();

                foreach (var ball in balls)
                {
                    string value =
                        ball.TextContent.Trim();

                    if (ball.ClassList.Contains("active"))
                        special.Add(value);
                    else
                        numbers.Add(value);
                }

                if (numbers.Count == 0)
                    continue;

                results.Add(
                    new VietlottResult
                    {
                        GameCode = gameCode,
                        DrawNo = drawMatch.Groups[1].Value,
                        DrawDate = DateTime.ParseExact(
                            dateMatch.Groups[1].Value,
                            "dd/MM/yyyy",
                            CultureInfo.InvariantCulture),
                        Numbers = numbers,
                        SpecialNumbers = special
                    });
            }
        }
        private void ParseBingo(IDocument document,List<VietlottResult> results)
        {
            var box = document.QuerySelector( ".box_kqtt_orange:has(img[alt='Bingo18'])");

            if (box == null)
                return;

            var rows =
                box.QuerySelectorAll(
                    "table tr");

            foreach (var row in rows)
            {
                var cells =
                    row.QuerySelectorAll("td");

                if (cells.Length < 4)
                    continue;

                string dateText =
                    cells[0]
                        .QuerySelector("a")
                        ?.TextContent
                        .Trim();

                string drawText =
                    cells[0]
                        .TextContent
                        .Trim();

                var dateMatch =
                    Regex.Match(
                        dateText ?? "",
                        @"\d{2}/\d{2}/\d{4}");

                var drawMatch =
                    Regex.Match(
                        drawText,
                        @"#(\d+)");

                if (!dateMatch.Success ||
                    !drawMatch.Success)
                    continue;

                var numbers =
                    cells[1]
                        .QuerySelectorAll(
                            ".bong_tron_bingo")
                        .Select(x =>
                            x.TextContent.Trim())
                        .ToList();

                if (numbers.Count == 0)
                    continue;

                int? total = null;

                int.TryParse(
                    cells[2]
                        .TextContent
                        .Trim(),
                    out int totalValue);

                total = totalValue;

                results.Add(
                    new VietlottResult
                    {
                        GameCode = "BINGO18",

                        DrawNo =
                            drawMatch.Groups[1].Value,

                        DrawDate =
                            DateTime.ParseExact(
                                dateMatch.Value,
                                "dd/MM/yyyy",
                                CultureInfo.InvariantCulture),

                        Numbers =
                            numbers,

                        Total =
                            total,

                        Size =
                            cells[3]
                                .TextContent
                                .Trim()
                    });
            }
        }
        private void ParseKeno(IDocument document,List<VietlottResult> results)
        {
            var box =
                document.QuerySelector(
                    ".box_kqtt_orange:has(img[alt='Keno'])");

            if (box == null)
                return;

            var rows =
                box.QuerySelectorAll(
                    "table tr");

            foreach (var row in rows)
            {
                var cells =
                    row.QuerySelectorAll("td");

                if (cells.Length < 4)
                    continue;

                string dateText =
                    cells[0]
                        .QuerySelector("a")
                        ?.TextContent
                        .Trim();

                string drawText =
                    cells[0]
                        .TextContent
                        .Trim();

                var dateMatch =
                    Regex.Match(
                        dateText ?? "",
                        @"\d{2}/\d{2}/\d{4}");

                var drawMatch =
                    Regex.Match(
                        drawText,
                        @"#(\d+)");

                if (!dateMatch.Success ||
                    !drawMatch.Success)
                    continue;

                var numbers =
                    cells[1]
                        .QuerySelectorAll(
                            ".day_so_ket_qua_v2 .bong_tron")
                        .Select(x =>
                            x.TextContent.Trim())
                        .ToList();

                if (numbers.Count != 20)
                    continue;

                string oddEven =
                    cells[2]
                        .TextContent
                        .Trim();

                string largeSmall =
                    cells[3]
                        .TextContent
                        .Trim();

                results.Add(
                    new VietlottResult
                    {
                        GameCode = "KENO",

                        DrawNo =
                            drawMatch.Groups[1].Value,

                        DrawDate =
                            DateTime.ParseExact(
                                dateMatch.Value,
                                "dd/MM/yyyy",
                                CultureInfo.InvariantCulture),

                        Numbers =
                            numbers,

                        OddEven =
                            oddEven,

                        Size =
                            largeSmall
                    });
            }
        }
        private List<VietlottPrize> ParsePrizeTable( IElement table)
        {
            var prizes =
                new List<VietlottPrize>();

            var rows =
                table.QuerySelectorAll("tbody tr");

            foreach (var row in rows)
            {
                var cells =
                    row.QuerySelectorAll("td");

                if (cells.Length < 2)
                    continue;

                string prizeName =
                    cells[0]
                        .TextContent
                        .Trim();

                var numbers =
                    cells[1]
                        .QuerySelectorAll(
                            "span.red.bold.large")
                        .Select(x =>
                            x.TextContent.Trim())
                        .Where(x =>
                            !string.IsNullOrEmpty(x))
                        .ToList();

                if (numbers.Count == 0)
                    continue;

                string value = "";

                if (cells.Length >= 3)
                {
                    value =
                        cells[2]
                            .TextContent
                            .Trim();
                }

                prizes.Add(
                    new VietlottPrize
                    {
                        PrizeName =
                            prizeName,

                        Numbers =
                            numbers,

                        Value =
                            value
                    });
            }

            return prizes;
        }
        private void ParseMax3D(IDocument document,List<VietlottResult> results)
        {
            var box =
                document.QuerySelector(
                    ".max3D_table");

            if (box == null)
                return;

            var main =
                box
                    .Closest(".box_kqtt");

            if (main == null)
                return;

            var h5 =
                box.QuerySelector(
                    "h5");

            if (h5 == null)
                h5 =
                    main.QuerySelector("h5");

            if (h5 == null)
                return;

            string header =
                h5.TextContent.Trim();

            var drawMatch =
                Regex.Match(
                    header,
                    @"#(\d+)");

            var dateMatch =
                Regex.Match(
                    header,
                    @"\d{2}/\d{2}/\d{4}");

            if (!drawMatch.Success ||
                !dateMatch.Success)
                return;

            var max3d =
                box.QuerySelector(
                    "#divMax3D");

            var max3dPlus =
                box.QuerySelector(
                    "#divMax3DPlus");

            if (max3d != null)
            {
                var table =
                    max3d.QuerySelector("table");

                if (table != null)
                {
                    results.Add(
                        new VietlottResult
                        {
                            GameCode = "MAX3D",
                            DrawNo =
                                drawMatch.Groups[1].Value,

                            DrawDate =
                                DateTime.ParseExact(
                                    dateMatch.Value,
                                    "dd/MM/yyyy",
                                    CultureInfo.InvariantCulture),

                            Prizes =
                                ParsePrizeTable(table)
                        });
                }
            }

            if (max3dPlus != null)
            {
                var table =
                    max3dPlus.QuerySelector("table");

                if (table != null)
                {
                    results.Add(
                        new VietlottResult
                        {
                            GameCode = "MAX3D_PLUS",

                            DrawNo =
                                drawMatch.Groups[1].Value,

                            DrawDate =
                                DateTime.ParseExact(
                                    dateMatch.Value,
                                    "dd/MM/yyyy",
                                    CultureInfo.InvariantCulture),

                            Prizes =
                                ParsePrizeTable(table)
                        });
                }
            }
        }
       private List<VietlottPrize> Parse3DProPrizeTable(
    IElement table)
{
    var prizes =
        new List<VietlottPrize>();

    var rows =
        table.QuerySelectorAll("tbody tr");

    foreach (var row in rows)
    {
        var cells =
            row.QuerySelectorAll("td");

        if (cells.Length < 2)
        {
            continue;
        }

        // ==========================================
        // TÊN GIẢI
        // ==========================================

        string prizeName =
            cells[0]
                .TextContent
                .Trim();

        if (string.IsNullOrWhiteSpace(
            prizeName))
        {
            continue;
        }

        // ==========================================
        // CÁC BỘ 3 SỐ
        // ==========================================

        var numbers =
            cells[1]
                .QuerySelectorAll(
                    "span.red.bold.large")
                .Select(x =>
                    x.TextContent.Trim())
                .Where(x =>
                    Regex.IsMatch(
                        x,
                        @"^\d{3}$"))
                .ToList();

        if (numbers.Count == 0)
        {
            continue;
        }

        // ==========================================
        // GIÁ TRỊ GIẢI
        // ==========================================

        string value = "";

        if (cells.Length >= 3)
        {
            value =
                cells[2]
                    .TextContent
                    .Trim();
        }

        // ==========================================
        // ADD
        // ==========================================

        prizes.Add(
            new VietlottPrize
            {
                PrizeName =
                    prizeName,

                Numbers =
                    numbers,

                Value =
                    value
            });
    }

    return prizes;
}
      private void ParseMax3DPro(
    IDocument document,
    List<VietlottResult> results)
{
    // ==========================================
    // TÌM KHỐI MAX 3D PRO
    // ==========================================

    var box =
        document.QuerySelector(
            ".Max3DPro_table");

    if (box == null)
    {
        return;
    }

    // ==========================================
    // H5 NẰM Ở BOX CHA
    // .box_kqtt_nd_chinh_home
    // ==========================================

    var parent =
        box.ParentElement;

    if (parent == null)
    {
        return;
    }

    var h5 =
        parent.QuerySelector("h5");

    if (h5 == null)
    {
        return;
    }

    // ==========================================
    // HEADER
    // ==========================================

    string header =
        h5.TextContent
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

    // ==========================================
    // DRAW NO
    // ==========================================

    var drawMatch =
        Regex.Match(
            header,
            @"#(\d+)");

    // ==========================================
    // DATE
    // ==========================================

    var dateMatch =
        Regex.Match(
            header,
            @"(\d{2}/\d{2}/\d{4})");

    if (!drawMatch.Success ||
        !dateMatch.Success)
    {
        return;
    }

    // ==========================================
    // TABLE
    // ==========================================

    var table =
        box.QuerySelector(
            "#divMax3DProPlus table");

    if (table == null)
    {
        return;
    }

    // ==========================================
    // PARSE PRIZE
    // ==========================================

    var prizes =
        Parse3DProPrizeTable(table);

    if (prizes == null ||
        prizes.Count == 0)
    {
        return;
    }

    // ==========================================
    // ADD RESULT
    // ==========================================

    results.Add(
        new VietlottResult
        {
            GameCode = "MAX3D_PRO",

            DrawNo =
                drawMatch.Groups[1].Value,

            DrawDate =
                DateTime.ParseExact(
                    dateMatch.Groups[1].Value,
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture),

            Numbers =
                new List<string>(),

            SpecialNumbers =
                new List<string>(),

            Prizes =
                prizes
        });
}
    }
}