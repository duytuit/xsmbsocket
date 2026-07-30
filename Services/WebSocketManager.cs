using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace xsmbsocket.Services
{
    public class WebSocketManager
    {
        private readonly ConcurrentDictionary<Guid, WebSocket> _clients
            = new();

        public void Add(Guid id, WebSocket socket)
        {
            _clients.TryAdd(id, socket);
        }

        public void Remove(Guid id)
        {
            _clients.TryRemove(id, out _);
        }

        public async Task BroadcastAsync(
            string message,
            CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach (var client in _clients)
            {
                if (client.Value.State != WebSocketState.Open)
                {
                    continue;
                }

                await client.Value.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    true,
                    token
                );
            }
        }
    }
}
