using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace xsmbsocket.Services
{
    public class HeartbeatService : BackgroundService
    {
        private readonly ILogger<HeartbeatService> _logger;
        private readonly WebSocketManager _manager;

        public HeartbeatService(
            ILogger<HeartbeatService> logger,
            WebSocketManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    $"Connections: {_manager.TotalConnections}"
                );

                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken
                );
            }
        }
    }
}