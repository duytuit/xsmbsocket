using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace xsmbsocket.Services
{
    public class LotteryResultCleanupService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ILogger<LotteryResultCleanupService> _logger;

        public LotteryResultCleanupService(
            IConfiguration configuration,
            ILogger<LotteryResultCleanupService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run once at startup, then every 24 hours. Draw dates use Vietnam time.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cutoff = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
                        .Date.AddMonths(-6);
                    await CleanupAsync(cutoff, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up old lottery results.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task CleanupAsync(DateTime cutoff, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Commit each batch separately to limit transaction size. Lock the selected
            // parent rows until their prizes and results have both been removed.
            const string vietlottSql = @"
                DECLARE @Ids TABLE (Id BIGINT PRIMARY KEY);
                INSERT INTO @Ids (Id)
                SELECT TOP (1000) Id
                FROM dbo.VietlottResults WITH (UPDLOCK, HOLDLOCK)
                WHERE DrawDate < @Cutoff
                ORDER BY Id;

                DELETE p
                FROM dbo.VietlottPrizes p
                INNER JOIN @Ids old ON old.Id = p.ResultId;
                DECLARE @Prizes INT = @@ROWCOUNT;

                DELETE r
                FROM dbo.VietlottResults r
                INNER JOIN @Ids old ON old.Id = r.Id;
                SELECT @@ROWCOUNT AS Results, @Prizes AS Prizes;";

            long vietlottCount = 0;
            long prizeCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var transaction = connection.BeginTransaction();
                var batch = await connection.QuerySingleAsync<CleanupBatch>(new CommandDefinition(
                    vietlottSql, new { Cutoff = cutoff }, transaction,
                    commandTimeout: 120, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                vietlottCount += batch.Results;
                prizeCount += batch.Prizes;
                if (batch.Results == 0)
                {
                    break;
                }
            }

            const string xoSoSql = @"
                DELETE TOP (1000) FROM dbo.XoSoResults WHERE DrawDate < @Cutoff;
                SELECT @@ROWCOUNT;";
            long xoSoCount = 0;
            while (true)
            {
                var deleted = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    xoSoSql, new { Cutoff = cutoff }, commandTimeout: 120,
                    cancellationToken: cancellationToken));
                xoSoCount += deleted;
                if (deleted == 0)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Lottery cleanup completed. Before {Cutoff:yyyy-MM-dd}: deleted {XoSoCount} XoSoResults, {VietlottCount} VietlottResults, {PrizeCount} VietlottPrizes.",
                cutoff, xoSoCount, vietlottCount, prizeCount);
        }

        private sealed class CleanupBatch
        {
            public int Results { get; set; }
            public int Prizes { get; set; }
        }
    }
}
