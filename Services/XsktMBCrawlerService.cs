using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xsmbsocket.Lotterys.Repositories;
using System.Net.Http;

namespace xsmbsocket.Services
{
    public class XsktMBCrawlerService : BackgroundService
    {
        private const string Url = "https://xosotructiep.vn/truc-tiep-xo-so/mien-bac.html";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly XoSoTrucTiepHtmlParser _parser;
        private readonly ILogger<XsktMBCrawlerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public XsktMBCrawlerService(
            IHttpClientFactory httpClientFactory,
            XoSoTrucTiepHtmlParser parser,
            ILogger<XsktMBCrawlerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _httpClientFactory = httpClientFactory;
            _parser = parser;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("XsktMBCrawlerService started.");
            await CrawlAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var startTime = now.Date.AddHours(16);
                var endTime = now.Date.AddHours(19).AddMinutes(15);
                if (now < startTime || now >= endTime)
                {
                    var nextStartTime = now < startTime
                        ? startTime
                        : startTime.AddDays(1);
                    await Task.Delay(nextStartTime - now, stoppingToken);
                    continue;
                }

                await CrawlAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        private async Task CrawlAsync(CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("XoSo");
                using var response = await client.GetAsync(Url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                var parsedResults = _parser.Parse(html)
                    .Where(result => result.DrawDate.Date == DateTime.Today &&
                        result.Region == "Miền Bắc")
                    .ToList();

                if (parsedResults.Count == 0)
                {
                    _logger.LogInformation(
                        "Xskt MB has no result for {Date}; waiting for the source page.",
                        DateTime.Today.ToString("dd/MM/yyyy"));
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<XoSoRepository>();
                foreach (var result in parsedResults)
                {
                    await repository.SaveAsync(result, cancellationToken);
                }

                _logger.LogInformation(
                    "Xskt MB crawl completed. Saved: {Count}; prizes: {PrizeCount}/8.",
                    parsedResults.Count,
                    parsedResults.Max(result => result.Prizes.Count));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Xskt crawl cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while crawling xosotructiep.vn.");
            }
        }
    }
}