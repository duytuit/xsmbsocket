using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Lotterys.Dtos;
using xsmbsocket.Lotterys.Models;
using xsmbsocket.Shares;
using xsmbsocket.Shares.BaseRepository;
using xsmbsocket.Shares.MysqlHelper;
using xsmbsocket.Shares.SqlServerHelper;

namespace xsmbsocket.Lotterys.Repositories
{
    public class LotteryRepositories : BaseRepository<Lottery>, ILotteryRepositories
    {
        private readonly XoSoDBContext _context;
        private readonly IConfiguration _configuration;
        private readonly RedisService _redis;
        public LotteryRepositories(XoSoDBContext context, IConfiguration configuration, RedisService redis) : base(context)
        {
            _context = context;
            _configuration = configuration;
            _redis = redis;
        }

        public Task<Lottery> CreateAsync(Lottery Lottery)
        {
            _context.Lotteries.Add(Lottery);
            _context.SaveChanges();
            return Task.FromResult(Lottery);
        }

        public Task<Lottery> DeleteSoftAsync(Lottery Lottery)
        {
            _context.Lotteries.Update(Lottery);
            _context.SaveChanges();
            return Task.FromResult(Lottery);
        }

        public async Task<PaginatedResultReact<object>> GetObjectTaskAsync(LotteryDto LotteryDto, int page, int pageSize, CancellationToken cancellationToken)
        {
            var sql = $@"
                    SELECT
                       l.*
                    FROM lotterys l
                    WHERE l.deleted_at IS NULL";
            if (LotteryDto.FromDate.HasValue && LotteryDto.ToDate.HasValue)
            {
                // Cộng thêm 1 ngày cho ToDate
                var toDateNext = LotteryDto.ToDate.Value.Date.AddDays(1);
                // Format chuẩn yyyy-MM-dd HH:mm:ss để SQL hiểu đúng
                sql += $@" AND l.created_at >= '{LotteryDto.FromDate.Value:yyyy-MM-dd}' 
                AND l.created_at < '{toDateNext:yyyy-MM-dd}'";
            }
            var results = await SqlServerHelpers.ExecuteQuerySqlAsync(_configuration.GetConnectionString("DefaultConnection"), sql, cancellationToken);
            var _results = new PaginatedResultReact<object>
            {
                Data = results,
            };
            return _results;
        }

        public Task<Lottery> ShowAsync(int id)
        {
            return _context.Lotteries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<Lottery> UpdateAsync(Lottery Lottery)
        {
            _context.Lotteries.Update(Lottery);
            _context.SaveChanges();
            return Task.FromResult(Lottery);
        }
    }
}
