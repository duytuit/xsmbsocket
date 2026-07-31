using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Models;
using xsmbsocket.Services;
using xsmbsocket.Shares;
using xsmbsocket.Shares.BaseRepository;
using xsmbsocket.Shares.Connects;

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
             services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                            .AllowAnyOrigin() // hoặc .WithOrigins("https://your-frontend.com")
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
            });
            // kết nối redis
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse(Configuration.GetConnectionString("Redis"), true);
                configuration.ResolveDns = true;
                return ConnectionMultiplexer.Connect(configuration);
            });
            // kết nối sql server
            services.AddDbContext<XoSoDBContext>(options =>
                {
                        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                }
            );
            // kết nối sql server kiểu ado
            services.Configure<ConnectionStrings>(Configuration.GetSection("ConnectionStrings"));
            services.AddTransient<AdoXoSoDB>();
            services.AddSingleton<RedisService>();
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
                    var socket = await context
                   .WebSockets
                   .AcceptWebSocketAsync();
                    var client = new ClientsInfo
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
                    var manager = context.RequestServices
                        .GetRequiredService<WebSocketManager>();

               


                    manager.Add(client);

                    var buffer = new byte[8192];

                    while (socket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var result = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Closed by client",
                                CancellationToken.None);
                            break;
                        }

                        client.LastSeen = DateTime.UtcNow;
                        client.ReceivedBytes += result.Count;
                    }

                    manager.Remove(client.Id);
                });
            });
        }
    }
}
