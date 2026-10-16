using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xsmbsocket.Lotterys.Repositories;
using System.Net.Http;

namespace xsmbsocket.Services
{
    public class XsktMTCrawlerService : BackgroundService
    {
        private const string Url = "https://xosotructiep.vn/truc-tiep-xo-so/mien-trung.html";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly XoSoTrucTiepHtmlParser _parser;
        private readonly ILogger<XsktMTCrawlerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public XsktMTCrawlerService(
            IHttpClientFactory httpClientFactory,
            XoSoTrucTiepHtmlParser parser,
            ILogger<XsktMTCrawlerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _httpClientFactory = httpClientFactory;
            _parser = parser;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("XsktMTCrawlerService started.");
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
                var results = _parser.Parse(html)
                    .Where(result => result.DrawDate.Date == DateTime.Today &&
                        result.Region == "Miền Trung")
                    .ToList();

                if (results.Count == 0)
                {
                    _logger.LogInformation(
                        "Xskt MT is waiting for the result of {Date} during the 16:00-19:15 crawl window. Site has not published it yet.",
                        DateTime.Today.ToString("dd/MM/yyyy"));
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<XoSoRepository>();
                foreach (var result in results)
                {
                    await repository.SaveAsync(result, cancellationToken);
                }

                _logger.LogInformation(
                    "Xskt MT crawl completed. Saved: {Count} provinces.",
                    results.Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Xskt MT crawl cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while crawling Central results from xosotructiep.vn.");
            }
        }
    }
}