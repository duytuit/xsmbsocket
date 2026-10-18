using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public class WebSocketManager
    {
        private readonly ConcurrentDictionary<Guid, ClientsInfo> _clients = new ConcurrentDictionary<Guid, ClientsInfo>();

        private readonly object _publishLock = new object();
        private readonly Dictionary<string, byte[]> _latest = new Dictionary<string, byte[]>();
        private DateTime _snapshotDate = DateTime.Today;
        private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _outboxes = new ConcurrentDictionary<Guid, Channel<byte[]>>();

        public int TotalConnections => _clients.Count;

        public void Add(ClientsInfo client)
        {
            lock (_publishLock)
            {
                ResetSnapshotForToday();
                var outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait
                });
                foreach (var snapshot in _latest.Values)
                    outbox.Writer.TryWrite(snapshot);
                _outboxes[client.Id] = outbox;
                _clients[client.Id] = client;
                // One sender per socket preserves ordering and isolates slow clients.
                _ = SendLoopAsync(client, outbox.Reader);
            }
        }

        public void Remove(Guid id)
        {
            if (_outboxes.TryRemove(id, out var outbox))
                outbox.Writer.TryComplete();
            if (_clients.TryRemove(id, out var client))
            {
                try
                {
                    client.Socket?.Dispose();
                }
                catch
                {
                }
            }
        }

        public List<ClientsInfo> GetClients()
        {
            return _clients.Values.ToList();
        }

        public Task BroadcastAsync(string message, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var bytes = Encoding.UTF8.GetBytes(message);
            lock (_publishLock)
            {
                ResetSnapshotForToday();
                // Heartbeats must never replace the latest result for a region.
                if (message.StartsWith("0|1") || message.StartsWith("0|2") || message.StartsWith("0|3"))
                    _latest[message.Substring(0, 3)] = bytes;

                foreach (var entry in _outboxes)
                {
                    // Disconnect on overflow instead of silently dropping result updates.
                    if (!entry.Value.Writer.TryWrite(bytes))
                        Remove(entry.Key);
                }
            }
            return Task.CompletedTask;
        }

        private void ResetSnapshotForToday()
        {
            if (_snapshotDate != DateTime.Today)
            {
                _latest.Clear();
                _snapshotDate = DateTime.Today;
            }
        }

        private async Task SendLoopAsync(ClientsInfo client, ChannelReader<byte[]> reader)
        {
            try
            {
                await foreach (var bytes in reader.ReadAllAsync())
                {
                    if (client.Socket?.State != WebSocketState.Open)
                        break;
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await client.Socket.SendAsync(new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text, true, timeout.Token);
                    client.LastSeen = DateTime.UtcNow;
                    client.SentBytes += bytes.Length;
                }
            }
            catch (Exception)
            {
                // Failed or slow sockets are removed; other clients keep receiving.
            }
            finally
            {
                Remove(client.Id);
            }
        }

        public void RemoveTimeoutClients()
        {
            // Activity is now tracked by socket receive/send events instead of
            // a hard one-minute timeout. This method is intentionally kept as a
            // no-op to avoid disconnecting healthy clients.
        }

        public void RemoveDisconnected()
        {
            var disconnected = new List<Guid>();

            foreach (var item in _clients)
            {
                var client = item.Value;

                if (client.Socket == null)
                {
                    disconnected.Add(client.Id);
                    continue;
                }

                if (client.Socket.State != WebSocketState.Open)
                {
                    disconnected.Add(client.Id);
                }
            }

            foreach (var id in disconnected)
            {
                Remove(id);
            }
        }
    }
}