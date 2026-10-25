using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using xsmbsocket.Services;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using xsmbsocket.Models;

namespace xsmbsocket.Lotterys.Repositories
{
    public class XoSoRepository
    {
        private readonly string _connectionString;
        public XoSoRepository(string connectionString) { _connectionString = connectionString; }

        public Task<bool> FillMissingPrizesAsync(XoSoResult result, CancellationToken cancellationToken)
            => WriteAsync(result, WriteMode.FillMissing, cancellationToken);

        public Task<bool> InsertIfMissingAsync(XoSoResult result, CancellationToken cancellationToken)
            => WriteAsync(result, WriteMode.InsertOnly, cancellationToken);

        public async Task SaveAsync(XoSoResult result, CancellationToken cancellationToken = default)
            => await WriteAsync(result, WriteMode.Replace, cancellationToken);

        private enum WriteMode { FillMissing, InsertOnly, Replace }

        private async Task<bool> WriteAsync(XoSoResult result, WriteMode mode, CancellationToken cancellationToken)
        {
            LotteryResultIdentity.Normalize(result);
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            // All writers lock the same date, even when sources use different station aliases.
            // Keep the database range locks as protection against older writers too.
            var lockResult = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
                DECLARE @status int;
                EXEC @status = sys.sp_getapplock @Resource = @resource,
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                SELECT @status;", new { resource = "XoSoResults:" + result.DrawDate.ToString("yyyyMMdd") },
                transaction: transaction, cancellationToken: cancellationToken));
            if (lockResult < 0) throw new InvalidOperationException("Could not lock the lottery draw for writing.");
            var rows = await connection.QueryAsync<StoredResult>(new CommandDefinition(@"
                SELECT Id, Region, ProvinceCode, ProvinceName, DrawDate, Prizes
                FROM dbo.XoSoResults WITH (UPDLOCK, HOLDLOCK)
                WHERE DrawDate >= @DrawDate AND DrawDate < DATEADD(day, 1, @DrawDate)
                ORDER BY UpdatedAt DESC, Id DESC;", new { result.DrawDate },
                transaction: transaction, cancellationToken: cancellationToken));
            // Existing duplicates must not make QuerySingle fail. Keep their original rows;
            // fill missing prizes into one preferred row without concatenating winning numbers.
            var matching = rows.Where(r => LotteryResultIdentity.Matches(r.Identity(), result)).ToList();
            var existing = matching.FirstOrDefault();
            if (existing != null && mode == WriteMode.InsertOnly) { transaction.Commit(); return false; }
            var prizes = existing == null ? new List<XoSoPrize>() : ReadPrizes(existing.Prizes);
            var original = JsonConvert.SerializeObject(prizes);
            foreach (var other in matching.Skip(1)) LotteryPrizeMerger.FillMissing(prizes, ReadPrizes(other.Prizes));
            if (mode == WriteMode.Replace)
            {
                var replacement = new List<XoSoPrize>();
                LotteryPrizeMerger.FillMissing(replacement, result.Prizes);
                LotteryPrizeMerger.FillMissing(replacement, prizes);
                prizes = replacement;
            }
            else LotteryPrizeMerger.FillMissing(prizes, result.Prizes);
            var json = JsonConvert.SerializeObject(prizes);
            var changed = existing == null ? prizes.Count > 0 : original != json;
            if (changed)
            {
                var sql = existing == null
                    ? @"INSERT INTO dbo.XoSoResults (Region, ProvinceCode, ProvinceName, DrawDate, Prizes)
                        VALUES (@Region, @ProvinceCode, @ProvinceName, @DrawDate, @Prizes);"
                    : @"UPDATE dbo.XoSoResults SET Prizes = @Prizes, UpdatedAt = GETDATE() WHERE Id = @Id;";
                await connection.ExecuteAsync(new CommandDefinition(sql, new {
                    Id = existing?.Id, result.Region, result.ProvinceCode, result.ProvinceName,
                    result.DrawDate, Prizes = json
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
            transaction.Commit();
            return changed;
        }

        private static List<XoSoPrize> ReadPrizes(string json)
            => string.IsNullOrWhiteSpace(json) ? new List<XoSoPrize>()
                : JsonConvert.DeserializeObject<List<XoSoPrize>>(json) ?? new List<XoSoPrize>();

        private sealed class StoredResult
        {
            public long Id { get; set; }
            public string Region { get; set; }
            public string ProvinceCode { get; set; }
            public string ProvinceName { get; set; }
            public DateTime DrawDate { get; set; }
            public string Prizes { get; set; }
            public XoSoResult Identity() => new XoSoResult {
                Region = Region, ProvinceCode = ProvinceCode, ProvinceName = ProvinceName, DrawDate = DrawDate
            };
        }
    }
}
