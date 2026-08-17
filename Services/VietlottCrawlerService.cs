using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Linq;
using xsmbsocket.Models;
using Microsoft.Extensions.DependencyInjection;
using xsmbsocket.Lotterys.Repositories;
namespace xsmbsocket.Services
{
    public class VietlottCrawlerService : BackgroundService
    {
        private readonly ILogger<VietlottCrawlerService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly VietlottHtmlParser _parser;
        private readonly IServiceScopeFactory _scopeFactory;

        public VietlottCrawlerService(
            ILogger<VietlottCrawlerService> logger,
            IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _parser = new VietlottHtmlParser();
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "==========================================");

            _logger.LogInformation(
                "VIETLOTT CRAWLER STARTED");

            _logger.LogInformation(
                "==========================================");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CrawlAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Application đang shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Vietlott crawler error.");
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromMinutes(5),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Vietlott crawler stopped.");
        }

        private async Task CrawlAsync(
            CancellationToken cancellationToken)
        {
            var client =
                _httpClientFactory.CreateClient("Vietlott");

            string url = "https://vietlott.vn/";

            _logger.LogInformation(
                "------------------------------------------");

            _logger.LogInformation(
                "Crawling: {Url}",
                url);

            // ==========================================
            // DOWNLOAD HTML
            // ==========================================

            using var response =
                await client.GetAsync(
                    url,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            string html =
                await response.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "HTTP Status: {StatusCode}",
                response.StatusCode);

            _logger.LogInformation(
                "HTML Length: {Length}",
                html.Length);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.LogWarning(
                    "HTML is empty.");

                return;
            }

            // ==========================================
            // ANGLESHARP
            // ==========================================

            var config =
                Configuration.Default;

            var context =
                BrowsingContext.New(config);

            var document =
                await context.OpenAsync(
                    req => req.Content(html));

            _logger.LogInformation(
                "HTML parsed successfully.");

            // ==========================================
            // PARSE
            // ==========================================

            var results =
                _parser.Parse(document);
            using var scope = _scopeFactory.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<VietlottRepository>();
            _logger.LogInformation(
                "Found {Count} Vietlott results.",
                results.Count);

            // ==========================================
            // LOG RESULT
            // ==========================================

            foreach (var item in results)
            {
                LogResult(item);
                try
                {
                    long id =
                        await repository.SaveResultAsync(
                            item,
                            cancellationToken);

                    _logger.LogInformation(
                        "DB SAVED: {Game} #{Draw} -> ID={Id}",
                        item.GameCode,
                        item.DrawNo,
                        id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "DB ERROR: {Game} #{Draw}",
                        item.GameCode,
                        item.DrawNo);
                }
            }

            _logger.LogInformation(
                "------------------------------------------");
        }

        private void LogResult(VietlottResult item)
        {
            _logger.LogInformation("");

            _logger.LogInformation(
                "==========================================");

            _logger.LogInformation(
                "GAME       : {Game}",
                item.GameCode);

            _logger.LogInformation(
                "DRAW       : #{Draw}",
                item.DrawNo);

            _logger.LogInformation(
                "DATE       : {Date}",
                item.DrawDate.ToString("dd/MM/yyyy"));

            // ==========================================
            // NUMBERS
            // LOTTO / MEGA / POWER / BINGO / KENO
            // ==========================================

            if (item.Numbers != null &&
                item.Numbers.Count > 0)
            {
                _logger.LogInformation(
                    "NUMBERS    : {Numbers}",
                    string.Join(
                        " ",
                        item.Numbers));
            }

            // ==========================================
            // SPECIAL
            // LOTTO / POWER
            // ==========================================

            if (item.SpecialNumbers != null &&
                item.SpecialNumbers.Count > 0)
            {
                _logger.LogInformation(
                    "SPECIAL    : {Special}",
                    string.Join(
                        " ",
                        item.SpecialNumbers));
            }

            // ==========================================
            // TOTAL
            // BINGO18
            // ==========================================

            if (item.Total.HasValue)
            {
                _logger.LogInformation(
                    "TOTAL      : {Total}",
                    item.Total.Value);
            }

            // ==========================================
            // ODD / EVEN
            // KENO
            // ==========================================

            if (!string.IsNullOrWhiteSpace(
                item.OddEven))
            {
                _logger.LogInformation(
                    "ODD/EVEN   : {OddEven}",
                    item.OddEven);
            }

            // ==========================================
            // LARGE / SMALL
            // BINGO18 / KENO
            // ==========================================

            if (!string.IsNullOrWhiteSpace(
                item.Size))
            {
                _logger.LogInformation(
                    "SIZE       : {Size}",
                    item.Size);
            }

            // ==========================================
            // PRIZES
            // MAX3D
            // MAX3D_PLUS
            // MAX3D_PRO
            // ==========================================

            if (item.Prizes != null &&
                item.Prizes.Count > 0)
            {
                _logger.LogInformation(
                    "PRIZES:");

                foreach (var prize in item.Prizes)
                {
                    string numbers = "";

                    if (prize.Numbers != null &&
                        prize.Numbers.Count > 0)
                    {
                        numbers =
                            string.Join(
                                ", ",
                                prize.Numbers);
                    }

                    if (!string.IsNullOrWhiteSpace(
                        prize.Value))
                    {
                        _logger.LogInformation(
                            "  {PrizeName}: {Numbers} - {Value}",
                            prize.PrizeName,
                            numbers,
                            prize.Value);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "  {PrizeName}: {Numbers}",
                            prize.PrizeName,
                            numbers);
                    }
                }
            }

            _logger.LogInformation(
                "==========================================");
        }
    }
}