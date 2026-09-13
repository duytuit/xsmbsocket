using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Threading;
using xsmbsocket.Models;
namespace xsmbsocket.Lotterys.Repositories
{
     public class VietlottRepository
    {
        private readonly string _connectionString;

        public VietlottRepository( IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<long> SaveResultAsync( VietlottResult item, CancellationToken cancellationToken)
        {
            using var connection = CreateConnection();

            if (connection is SqlConnection sqlConnection)
            {
                await sqlConnection.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }

            // ==========================================
            // TÌM KỲ ĐÃ CÓ
            // ==========================================

            var resultId =
                await connection.ExecuteScalarAsync<long?>(
                    @"
                    SELECT Id
                    FROM dbo.VietlottResults
                    WHERE GameCode = @GameCode
                      AND DrawNo = @DrawNo
                    ",
                    new
                    {
                        item.GameCode,
                        item.DrawNo
                    });

            // ==========================================
            // INSERT
            // ==========================================

            if (!resultId.HasValue)
            {
                resultId = await connection.ExecuteScalarAsync<long>(
                        @"
                        INSERT INTO dbo.VietlottResults
                        (
                            GameCode,
                            DrawNo,
                            DrawDate,
                            Numbers,
                            SpecialNumbers,
                            Total,
                            OddEven,
                            Size
                        )
                        VALUES
                        (
                            @GameCode,
                            @DrawNo,
                            @DrawDate,
                            @Numbers,
                            @SpecialNumbers,
                            @Total,
                            @OddEven,
                            @Size
                        );

                        SELECT CAST(
                            SCOPE_IDENTITY()
                            AS BIGINT);
                        ",
                        new
                        {
                            item.GameCode,
                            item.DrawNo,
                            item.DrawDate,

                            Numbers =
                                item.Numbers != null
                                    ? string.Join(
                                        ",",
                                        item.Numbers)
                                    : null,

                            SpecialNumbers =
                                item.SpecialNumbers != null
                                    ? string.Join(
                                        ",",
                                        item.SpecialNumbers)
                                    : null,

                            item.Total,
                            item.OddEven,
                            item.Size
                        });

                // Lưu giải
                await SavePrizesAsync( connection, resultId.Value, item);

                return resultId.Value;
            }

            // ==========================================
            // UPDATE
            // ==========================================

            await connection.ExecuteAsync(
                @"
                UPDATE dbo.VietlottResults
                SET
                    DrawDate = @DrawDate,
                    Numbers = @Numbers,
                    SpecialNumbers = @SpecialNumbers,
                    Total = @Total,
                    OddEven = @OddEven,
                    Size = @Size
                WHERE Id = @Id
                ",
                new
                {
                    Id = resultId.Value,

                    item.DrawDate,

                    Numbers =
                        item.Numbers != null
                            ? string.Join(
                                ",",
                                item.Numbers)
                            : null,

                    SpecialNumbers =
                        item.SpecialNumbers != null
                            ? string.Join(
                                ",",
                                item.SpecialNumbers)
                            : null,

                    item.Total,
                    item.OddEven,
                    item.Size
                });
            if (resultId != null && (item.GameCode == "MAX3D" || item.GameCode == "MAX3D_PLUS" || item.GameCode == "MAX3D_PRO"))
            {
                return resultId.Value;
            }
            // ==========================================
            // XÓA GIẢI CŨ
            // ==========================================

            await connection.ExecuteAsync(
                @"
                DELETE FROM dbo.VietlottPrizes
                WHERE ResultId = @ResultId
                ",
                new
                {
                    ResultId = resultId.Value
                });

            // ==========================================
            // INSERT GIẢI MỚI
            // ==========================================

            await SavePrizesAsync( connection, resultId.Value,  item);

            return resultId.Value;
        }

        private async Task SavePrizesAsync( IDbConnection connection, long resultId, VietlottResult item)
        {
            if (item.Prizes == null || item.Prizes.Count == 0)
            {
                return;
            }

            foreach (var prize in item.Prizes)
            {
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO dbo.VietlottPrizes
                    (
                        ResultId,
                        PrizeName,
                        Numbers,
                        PrizeValue
                    )
                    VALUES
                    (
                        @ResultId,
                        @PrizeName,
                        @Numbers,
                        @PrizeValue
                    )
                    ",
                    new
                    {
                        ResultId = resultId,

                        PrizeName =
                            prize.PrizeName,

                        Numbers =
                            prize.Numbers != null
                                ? string.Join(
                                    ",",
                                    prize.Numbers)
                                : null,

                        PrizeValue =
                            prize.Value
                    });
            }
        }
    }
}