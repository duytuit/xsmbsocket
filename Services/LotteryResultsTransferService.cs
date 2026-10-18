using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xsmbsocket.Lotterys.Repositories;
using xsmbsocket.Shares.BaseRepository;

namespace xsmbsocket.Services
{
    public class LotteryResultsTransferService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<LotteryResultsTransferService> _logger;
        private static readonly TimeZoneInfo VietnamTime = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public LotteryResultsTransferService(IServiceScopeFactory scopes, ILogger<LotteryResultsTransferService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await TransferAsync(stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime);
                    var start = now.Date.AddHours(16);
                    var end = now.Date.AddHours(19);
                    if (now < start || now >= end)
                    {
                        var next = now < start ? start : start.AddDays(1);
                        await Task.Delay(next - now, stoppingToken);
                        continue;
                    }
                    await TransferAsync(stoppingToken);
                    now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime);
                    var delay = end - now;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay < TimeSpan.FromMinutes(1) ? delay : TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }

        private async Task TransferAsync(CancellationToken token)
        {
            try
            {
                var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime).Date;
                var tomorrow = today.AddDays(1);
                using var scope = _scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<XoSoDBContext>();
                var repository = scope.ServiceProvider.GetRequiredService<XoSoRepository>();
                var rows = await context.LotteryResults.AsNoTracking()
                    .Where(r => r.CreatedAt >= today && r.CreatedAt < tomorrow &&
                        (r.Type == "mienbac" || r.Type == "miennam" || r.Type == "mientrung"))
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ToListAsync(token);
                var changed = 0;
                foreach (var row in rows)
                {
                    foreach (var result in LotteryResultsParser.Parse(row.Data, row.Type, today))
                        if (await repository.FillMissingPrizesAsync(result, token)) changed++;
                }
                if (changed > 0)
                    _logger.LogInformation("Inserted or filled missing prizes in {Count} lottery results for {Date}.", changed, today);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not transfer LotteryResults to XoSoResults; will retry during the scheduled window.");
            }
        }
    }
}
