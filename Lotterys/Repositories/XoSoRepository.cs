using System;
using System.Data;
using System.Collections.Generic;
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

        public XoSoRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<bool> FillMissingPrizesAsync(XoSoResult result, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            var parameters = new { result.Region, result.ProvinceName, DrawDate = result.DrawDate.Date };
            var existing = await connection.QuerySingleOrDefaultAsync<StoredResult>(new CommandDefinition(@"
                SELECT Id, Prizes FROM dbo.XoSoResults WITH (UPDLOCK, HOLDLOCK)
                WHERE Region = @Region AND ProvinceName = @ProvinceName
                  AND DrawDate >= @DrawDate AND DrawDate < DATEADD(day, 1, @DrawDate);",
                parameters, transaction: transaction, cancellationToken: cancellationToken));
            var prizes = existing == null || string.IsNullOrWhiteSpace(existing.Prizes)
                ? new List<XoSoPrize>()
                : JsonConvert.DeserializeObject<List<XoSoPrize>>(existing.Prizes) ?? new List<XoSoPrize>();
            var changed = LotteryPrizeMerger.FillMissing(prizes, result.Prizes);
            if (changed)
            {
                var sql = existing == null
                    ? @"INSERT INTO dbo.XoSoResults (Region, ProvinceCode, ProvinceName, DrawDate, Prizes)
                        VALUES (@Region, @ProvinceCode, @ProvinceName, @DrawDate, @Prizes);"
                    : @"UPDATE dbo.XoSoResults SET Prizes = @Prizes, UpdatedAt = GETDATE() WHERE Id = @Id;";
                await connection.ExecuteAsync(new CommandDefinition(sql, new
                {
                    Id = existing?.Id, result.Region, result.ProvinceCode, result.ProvinceName,
                    DrawDate = result.DrawDate.Date, Prizes = JsonConvert.SerializeObject(prizes)
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
            transaction.Commit();
            return changed;
        }

        private sealed class StoredResult
        {
            public long Id { get; set; }
            public string Prizes { get; set; }
        }

        public async Task<bool> InsertIfMissingAsync(XoSoResult result, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            // Range locks keep the existence check and insert atomic across service instances.
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            const string sql = @"
                INSERT INTO dbo.XoSoResults (Region, ProvinceCode, ProvinceName, DrawDate, Prizes)
                SELECT @Region, @ProvinceCode, @ProvinceName, @DrawDate, @Prizes
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.XoSoResults WITH (UPDLOCK, HOLDLOCK)
                    WHERE Region = @Region AND ProvinceCode = @ProvinceCode
                      AND DrawDate >= @DrawDate AND DrawDate < DATEADD(day, 1, @DrawDate)
                );";
            var inserted = await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                result.Region, result.ProvinceCode, result.ProvinceName,
                DrawDate = result.DrawDate.Date,
                Prizes = JsonConvert.SerializeObject(result.Prizes)
            }, transaction: transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return inserted > 0;
        }

        public async Task SaveAsync(
            XoSoResult result,
            CancellationToken cancellationToken = default)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            var prizesJson = JsonConvert.SerializeObject(
                result.Prizes);

            const string sql = @"
            IF EXISTS
            (
                SELECT 1
                FROM dbo.XoSoResults
                WHERE Region = @Region
                AND ProvinceCode = @ProvinceCode
                AND CONVERT(date, DrawDate) = @DrawDate
            )
            BEGIN

                UPDATE dbo.XoSoResults
                SET
                    ProvinceName = @ProvinceName,
                    Prizes = @Prizes,
                    UpdatedAt = GETDATE()
                WHERE Region = @Region
                AND ProvinceCode = @ProvinceCode
                AND CONVERT(date, DrawDate) = @DrawDate;

            END
            ELSE
            BEGIN

                INSERT INTO dbo.XoSoResults
                (
                    Region,
                    ProvinceCode,
                    ProvinceName,
                    DrawDate,
                    Prizes
                )
                VALUES
                (
                    @Region,
                    @ProvinceCode,
                    @ProvinceName,
                    @DrawDate,
                    @Prizes
                );

            END
            ";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        result.Region,
                        result.ProvinceCode,
                        result.ProvinceName,
                        DrawDate = result.DrawDate.Date,
                        Prizes = prizesJson
                    },
                    cancellationToken: cancellationToken));
        }
    }

}
