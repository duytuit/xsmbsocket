using System;
using System.Data;
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