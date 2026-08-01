using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Services
{
    public class ClientCleanupService : BackgroundService
    {
        private readonly WebSocketManager _manager;

        public ClientCleanupService(WebSocketManager manager)
        {
            _manager = manager;
        }

        protected override async Task ExecuteAsync( CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _manager.RemoveDisconnected();

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}