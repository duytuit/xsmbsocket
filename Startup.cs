using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Models;
using xsmbsocket.Services;

namespace xsmbsocket
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WebSocketManager>();
            services.AddHostedService<LiveSocketService>();
            services.AddHostedService<ClientCleanupService>();
            services.AddHostedService<HeartbeatService>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
      IApplicationBuilder app,
      IWebHostEnvironment env)
        {

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                endpoints.Map("/ws", async context =>
                {
                    if (!context.WebSockets.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }
                    var origin = context.Request.Headers["Origin"].ToString();

                    if (origin != "https://xosodaiphat.com")
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }
                    var manager = context.RequestServices
                        .GetRequiredService<WebSocketManager>();

                    var socket = await context
                        .WebSockets
                        .AcceptWebSocketAsync();

                    var client = new ClientInfo
                    {
                        Id = Guid.NewGuid(),
                        Socket = socket,
                        ConnectedAt = DateTime.Now,
                        LastSeen = DateTime.Now,
                        IpAddress = context.Connection
                            .RemoteIpAddress?
                            .ToString(),
                        SentBytes = 0,
                        ReceivedBytes = 0
                    };

                    manager.Add(client);

                    Console.WriteLine(
                        $"Connected: {client.Id} - {client.IpAddress}");

                    try
                    {
                        while (
                    socket.State == WebSocketState.Open &&
                    !context.RequestAborted.IsCancellationRequested)
                        {
                            await Task.Delay(5000);
                        }
                    }
                    finally
                    {
                        manager.Remove(client.Id);

                        try
                        {
                            socket.Dispose();
                        }
                        catch
                        {
                        }
                    }
                });
            });
        }
    }
}
