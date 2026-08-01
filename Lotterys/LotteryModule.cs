using Microsoft.Extensions.DependencyInjection;
using xsmbsocket.Lotterys.Repositories;

namespace xsmbsocket.Lotterys
{
    public static class LotteryModule
    {
        public static IServiceCollection AddLotteryModule(this IServiceCollection services)
        {
            services.AddScoped<ILotteryRepositories, LotteryRepositories>();
            return services;
        }
    }
}
