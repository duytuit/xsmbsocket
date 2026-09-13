using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xsmbsocket.Models;
using xsmbsocket.Lotterys.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace xsmbsocket.Services
{
    public class XoSoCrawlerService : BackgroundService
    {
        private const string Url = "https://xosotructiep.vn/";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly XoSoTrucTiepHtmlParser _parser;
        private readonly ILogger<XoSoCrawlerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public XoSoCrawlerService(
            IHttpClientFactory httpClientFactory,
            XoSoTrucTiepHtmlParser parser,
            ILogger<XoSoCrawlerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _httpClientFactory = httpClientFactory;
            _parser = parser;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "XoSoCrawlerService started.");

            // Crawl một lần ngay khi ứng dụng khởi động.
            await CrawlAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var startTime = now.Date.AddHours(16);
                var endTime = now.Date.AddHours(20);

                if (now < startTime || now >= endTime)
                {
                    var nextStartTime = now < startTime
                        ? startTime
                        : startTime.AddDays(1);

                    _logger.LogInformation(
                        "XoSo crawler is outside its schedule. " +
                        "Next crawl window starts at {NextStartTime}.",
                        nextStartTime);

                    await Task.Delay(
                        nextStartTime - now,
                        stoppingToken);

                    continue;
                }

                await CrawlAsync(stoppingToken);

                await Task.Delay(
                    TimeSpan.FromMinutes(2),
                    stoppingToken);
            }

            _logger.LogInformation(
                "XoSoCrawlerService stopped.");
        }

        private async Task CrawlAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Starting crawl: {Url}",
                    Url);

                var client =
                    _httpClientFactory.CreateClient("XoSo");

                using var response =
                    await client.GetAsync(
                        Url,
                        cancellationToken);

                response.EnsureSuccessStatusCode();

                var html =
                    await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogWarning(
                        "XoSo website returned empty HTML.");

                    return;
                }

                _logger.LogInformation(
                    "Downloaded XoSo HTML. Length: {Length}",
                    html.Length);

                var results = _parser.Parse(html);

                if (results == null || results.Count == 0)
                {
                    _logger.LogWarning(
                        "Parser returned no lottery results.");

                    return;
                }

                _logger.LogInformation(
                    "Parser found {Count} lottery results.",
                    results.Count);

                var validResults =
                    FilterValidResults(results);

                if (validResults.Count == 0)
                {
                    _logger.LogWarning(
                        "No valid lottery results after date validation.");

                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<XoSoRepository>();

                foreach (var result in validResults)
                {
                    await SaveResultAsync(
                        result,
                        repository,
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Crawl completed successfully. Saved: {Count}",
                    validResults.Count);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "XoSo crawl cancelled.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "HTTP error while crawling XoSo.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(
                    ex,
                    "HTTP request timeout while crawling XoSo.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while crawling XoSo.");
            }
        }

        private List<XoSoResult> FilterValidResults(
            List<XoSoResult> results)
        {
            var today = DateTime.Today;

            var validResults =
                results
                    .Where(IsValidResult)
                    .Where(x => x.DrawDate.Date <= today)
                    .ToList();

            foreach (var result in results)
            {
                if (result.DrawDate.Date > today)
                {
                    _logger.LogWarning(
                        "Skip future lottery result. " +
                        "Region: {Region}, Province: {Province}, Date: {Date}",
                        result.Region,
                        result.ProvinceName,
                        result.DrawDate.ToString("yyyy-MM-dd"));
                }
            }

            return validResults;
        }

        private bool IsValidResult(
            XoSoResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.Region))
            {
                _logger.LogWarning(
                    "Skip result because Region is empty.");

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                result.ProvinceCode))
            {
                _logger.LogWarning(
                    "Skip result because ProvinceCode is empty. " +
                    "Region: {Region}",
                    result.Region);

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                result.ProvinceName))
            {
                _logger.LogWarning(
                    "Skip result because ProvinceName is empty. " +
                    "Region: {Region}, Code: {Code}",
                    result.Region,
                    result.ProvinceCode);

                return false;
            }

            if (result.DrawDate == DateTime.MinValue)
            {
                _logger.LogWarning(
                    "Skip result because DrawDate is invalid. " +
                    "Region: {Region}, Province: {Province}",
                    result.Region,
                    result.ProvinceName);

                return false;
            }

            if (result.Prizes == null ||
                result.Prizes.Count == 0)
            {
                _logger.LogWarning(
                    "Skip result because no prizes were found. " +
                    "Region: {Region}, Province: {Province}, Date: {Date}",
                    result.Region,
                    result.ProvinceName,
                    result.DrawDate.ToString("yyyy-MM-dd"));

                return false;
            }

            return true;
        }

        private async Task SaveResultAsync(
            XoSoResult result,
            XoSoRepository repository,
            CancellationToken cancellationToken)
        {
            try
            {
                await repository.SaveAsync(
                    result,
                    cancellationToken);

                _logger.LogInformation(
                    "Saved lottery result. " +
                    "Region: {Region}, Code: {Code}, " +
                    "Province: {Province}, Date: {Date}, " +
                    "Prizes: {PrizeCount}",
                    result.Region,
                    result.ProvinceCode,
                    result.ProvinceName,
                    result.DrawDate.ToString("yyyy-MM-dd"),
                    result.Prizes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save lottery result. " +
                    "Region: {Region}, Code: {Code}, " +
                    "Province: {Province}, Date: {Date}",
                    result.Region,
                    result.ProvinceCode,
                    result.ProvinceName,
                    result.DrawDate.ToString("yyyy-MM-dd"));

                throw;
            }
        }
    }
}