using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Lotterys.Models;
using xsmbsocket.Lotterys.Repositories;
using xsmbsocket.Shares;

namespace xsmbsocket.Services
{
    public class LiveSocketService : BackgroundService
    {
        private const string PendingResultsQueueKey = "live-socket:lottery-results:pending";
        private ClientWebSocket _socket;

        private readonly ILogger<LiveSocketService> _logger;
        private readonly WebSocketManager _manager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RedisService _redis;

        public LiveSocketService(
            ILogger<LiveSocketService> logger,
            WebSocketManager manager,
            IServiceScopeFactory scopeFactory,
            RedisService redis)
        {
            _logger = logger;
            _manager = manager;
            _scopeFactory = scopeFactory;
            _redis = redis;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var windowStart = now.Date.AddHours(16);
                var windowEnd = now.Date.AddHours(19);

                if (now < windowStart || now >= windowEnd)
                {
                    if (_socket != null && _socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Outside allowed window", stoppingToken);
                        }
                        catch
                        {
                            // Ignore close errors while waiting for the next window.
                        }
                    }

                    var nextStartTime = now < windowStart
                        ? windowStart
                        : windowStart.AddDays(1);

                    var delay = nextStartTime - now;
                    _logger.LogInformation(
                        "LiveSocketService is outside its available window. " +
                        "Waiting until {NextStartTime} (exact 16:00:00) before reconnecting.",
                        nextStartTime);

                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

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
                    _logger.LogWarning(ex, "LiveSocketService connection loop error.");
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

            _socket.Options.SetRequestHeader( "Origin", "https://xosodaiphat.com" );

            await _socket.ConnectAsync( new Uri("wss://livewk.xosodaiphat.com"),token );
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[8192];

            while (_socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var result = await _socket.ReceiveAsync( new ArraySegment<byte>(buffer), token );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    var message = Encoding.UTF8.GetString( buffer,0, result.Count);

                    await _manager.BroadcastAsync( message, token);
                    _logger.LogInformation("Received message: {Message}", message);
                    if (message != "0")
                    {
                        await SaveOrQueueAsync(message, DateTime.Now, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError( ex, "Error receiving WebSocket message" );

                    break;
                }
            }
        }

        private async Task SaveOrQueueAsync(string message, DateTime receivedAt, CancellationToken token)
        {
            var pendingResult = new PendingLotteryResult
            {
                Data = message,
                Type = GetLotteryType(message),
                ReceivedAt = receivedAt
            };
            var serializedResult = JsonSerializer.Serialize(pendingResult);

            string queuedResult = null;
            try
            {
                queuedResult = await _redis.PeekQueueAsync(PendingResultsQueueKey, token);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogWarning(ex, "Could not inspect the Redis pending-results queue.");
            }

            if (queuedResult != null)
            {
                try
                {
                    await _redis.EnqueueAsync(PendingResultsQueueKey, serializedResult, token);
                    await DrainPendingQueueAsync(token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not process the Redis pending-results queue.");
                }

                return;
            }

            try
            {
                await SaveLotteryResultAsync(pendingResult);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save lottery result to SQL Server; queueing it in Redis.");
                try
                {
                    await _redis.EnqueueAsync(PendingResultsQueueKey, serializedResult, token);
                }
                catch (Exception redisException) when (!(redisException is OperationCanceledException))
                {
                    _logger.LogError(redisException, "Could not queue the lottery result in Redis.");
                }
            }
        }

        private async Task DrainPendingQueueAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var queuedResult = await _redis.PeekQueueAsync(PendingResultsQueueKey, token);
                if (queuedResult == null)
                {
                    return;
                }

                PendingLotteryResult pendingResult;
                try
                {
                    pendingResult = JsonSerializer.Deserialize<PendingLotteryResult>(queuedResult);
                    if (pendingResult == null)
                    {
                        throw new JsonException("Redis queue item was empty.");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Discarding an invalid item from the Redis pending-results queue.");
                    await _redis.RemoveQueueItemAsync(PendingResultsQueueKey, queuedResult, token);
                    continue;
                }

                try
                {
                    await SaveLotteryResultAsync(pendingResult);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SQL Server is still unavailable; pending results remain in Redis.");
                    return;
                }

                await _redis.RemoveQueueItemAsync(PendingResultsQueueKey, queuedResult, token);
            }
        }

        private async Task SaveLotteryResultAsync(PendingLotteryResult pendingResult)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILotteryRepositories>();
            var existingResult = await repository.ShowLotteryResultByTypeAndDateAsync(
                pendingResult.Type,
                pendingResult.ReceivedAt);

            if (existingResult != null)
            {
                existingResult.Data = pendingResult.Data;
                existingResult.UpdatedAt = pendingResult.ReceivedAt;
                await repository.UpdateLotteryResultAsync(existingResult);
                return;
            }

            var lotteryResult = new LotteryResult
            {
                Data = pendingResult.Data,
                Type = pendingResult.Type,
                CreatedAt = pendingResult.ReceivedAt,
                UpdatedAt = pendingResult.ReceivedAt
            };
            await repository.CreateLotteryResultAsync(lotteryResult);
        }

        private static string GetLotteryType(string message)
        {
            if (message.StartsWith("0|1"))
            {
                return "mienbac";
            }

            if (message.StartsWith("0|2"))
            {
                return "miennam";
            }

            if (message.StartsWith("0|3"))
            {
                return "mientrung";
            }

            return string.Empty;
        }

        private sealed class PendingLotteryResult
        {
            public string Data { get; set; }
            public string Type { get; set; }
            public DateTime ReceivedAt { get; set; }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_socket != null && _socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync( WebSocketCloseStatus.NormalClosure,"Service stopping",cancellationToken);
                }
            }
            catch
            {
                // Ignore socket close errors
            }

            _socket?.Dispose();

            await base.StopAsync(cancellationToken);
        }
        private static int CountOccurrences(string text, string search)
        {
            if (string.IsNullOrEmpty(text) ||
                string.IsNullOrEmpty(search))
            {
                return 0;
            }

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(search, index)) >= 0)
            {
                count++;
                index += search.Length;
            }

            return count;
        }
    }
}