using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using Serilog;

namespace xsmbsocket
{
    public class Program
    {
        public static void Main(string[] args)
        {
         

          Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    path: "Logs/log-.txt",                // đường dẫn log
                    rollingInterval: RollingInterval.Day, // mỗi ngày 1 file
                    retainedFileCountLimit: 7,            // giữ tối đa 7 ngày
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                Log.Information("Log directory: {LogDirectory}", "Logs");

                CreateHostBuilder(args)
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}