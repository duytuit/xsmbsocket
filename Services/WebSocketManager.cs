using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Models;

namespace xsmbsocket.Services
{
    public class WebSocketManager
    {
        private readonly ConcurrentDictionary<Guid, ClientInfo> _clients
            = new ConcurrentDictionary<Guid, ClientInfo>();

        public int TotalConnections => _clients.Count;

        public void Add(ClientInfo client)
        {
            _clients.TryAdd(client.Id, client);
        }

        public void Remove(Guid id)
        {
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

        public List<ClientInfo> GetClients()
        {
            return _clients.Values.ToList();
        }

        public async Task BroadcastAsync(
     string message,
     CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);

            var disconnected = new List<Guid>();

            foreach (var client in _clients.Values)
            {
                try
                {
                    if (client.Socket == null)
                    {
                        disconnected.Add(client.Id);
                        continue;
                    }

                    if (client.Socket.State != WebSocketState.Open)
                    {
                        disconnected.Add(client.Id);
                        continue;
                    }

                    await client.Socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        token
                    );

                    client.LastSeen = DateTime.UtcNow;
                    client.SentBytes += bytes.Length;
                }
                catch
                {
                    disconnected.Add(client.Id);
                }
            }

            foreach (var id in disconnected)
            {
                Remove(id);
            }
        }

        public void RemoveTimeoutClients()
        {
            var timeout = DateTime.Now.AddMinutes(-1);

            foreach (var item in _clients.Values)
            {
                if (item.LastSeen < timeout)
                {
                    Remove(item.Id);
                }
            }
        }
        public void RemoveDisconnected()
        {
            var timeout = DateTime.Now.AddMinutes(-1);

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
                    continue;
                }

                if (client.LastSeen < timeout)
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