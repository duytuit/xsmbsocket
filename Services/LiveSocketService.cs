using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Services
{
    public class LiveSocketService : BackgroundService
    {
        private ClientWebSocket _socket;
        private readonly ILogger<LiveSocketService> _logger;
        private readonly WebSocketManager _manager;

        public LiveSocketService(
            ILogger<LiveSocketService> logger,
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
                try
                {
                    await ConnectAsync(stoppingToken);
                    await ReceiveLoop(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task ConnectAsync(
            CancellationToken token)
        {
            _socket?.Dispose();

            _socket = new ClientWebSocket();

            _socket.Options.SetRequestHeader(
                "Origin",
                "https://xosodaiphat.com"
            );

            await _socket.ConnectAsync(
                new Uri("wss://livewk.xosodaiphat.com"),
                token
            );
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[8192];

            while (_socket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        token
                    );

                    if (result.MessageType ==
                        WebSocketMessageType.Close)
                    {
                        break;
                    }

                    var message = Encoding.UTF8.GetString(
                        buffer,
                        0,
                        result.Count
                    );

                    await _manager.BroadcastAsync(
                        message,
                        token
                    );
                }
                catch
                {
                    break;
                }
            }
        }
    }
}
