using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Lotterys.Dtos;
using xsmbsocket.Lotterys.Models;
using xsmbsocket.Shares;
using xsmbsocket.Shares.BaseRepository;

namespace xsmbsocket.Lotterys.Repositories
{
    public interface ILotteryRepositories : IBaseRepository<Lottery>
    {
        Task<PaginatedResultReact<object>> GetObjectTaskAsync(LotteryDto LotteryDto, int page, int pageSize, CancellationToken cancellationToken);
        Task<Lottery> ShowAsync(int id);
        Task<Lottery> CreateAsync(Lottery Lottery);
        Task<Lottery> UpdateAsync(Lottery Lottery);
        Task<Lottery> DeleteSoftAsync(Lottery Lottery);
    }
}
