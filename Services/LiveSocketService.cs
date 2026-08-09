using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Lotterys.Models;
using xsmbsocket.Lotterys.Repositories;

namespace xsmbsocket.Services
{
    public class LiveSocketService : BackgroundService
    {
        private ClientWebSocket _socket;

        private readonly ILogger<LiveSocketService> _logger;
        private readonly WebSocketManager _manager;
        private readonly IServiceScopeFactory _scopeFactory;

        public LiveSocketService(
            ILogger<LiveSocketService> logger,
            WebSocketManager manager,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _manager = manager;
            _scopeFactory = scopeFactory;
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
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // _logger.LogError(ex, "LiveSocketService error");
                }

                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ConnectAsync(CancellationToken token)
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

            while (_socket.State == WebSocketState.Open &&
                   !token.IsCancellationRequested)
            {
                try
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
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
                      _logger.LogInformation("Received message: {Message}", message);
                    if (message != "0")
                    {
                        var now = DateTime.Now;

                        var lottery = new Lottery
                        {
                            Data = message,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        // //Tạo scope cho Scoped Repository
                        using var scope = _scopeFactory.CreateScope();

                        var repoLottery =
                            scope.ServiceProvider
                                .GetRequiredService<ILotteryRepositories>();

                        await repoLottery.CreateAsync(lottery);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error receiving WebSocket message"
                    );

                    break;
                }
            }
        }

        public override async Task StopAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                if (_socket != null &&
                    _socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Service stopping",
                        cancellationToken
                    );
                }
            }
            catch
            {
                // Ignore socket close errors
            }

            _socket?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}