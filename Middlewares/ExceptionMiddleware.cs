using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Security.Claims;
using System.IO;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using xsmbsocket.Shares;
using xsmbsocket.Controllers;

namespace xsmbsocket.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }
        private static bool IsFileUpload(HttpRequest request)
        {
            return request.ContentType != null &&
                request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);
        }
        private async Task<string> ReadRequestBodyAsync(HttpContext context)
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            string body = await reader.ReadToEndAsync();

            context.Request.Body.Position = 0; // reset để controller còn đọc

            return body;
        }
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            int statusCode = (int)HttpStatusCode.InternalServerError;

            try
            {
                var request = context.Request;
                string apiInfo = $"{request.Method} {request.Path}{request.QueryString}";

                // 🔹 UserId
                string userId = context.User?
                    .Claims?
                    .FirstOrDefault(c =>
                        c.Type == ClaimTypes.NameIdentifier ||
                        c.Type == JwtRegisteredClaimNames.Sub ||
                        c.Type == "userId")
                    ?.Value ?? "Anonymous";

                // 🔹 POST body (BỎ upload file)
                string requestBody = "";

                bool isWriteBodyMethod =
                    request.Method == HttpMethods.Post ||
                    request.Method == HttpMethods.Put ||
                    request.Method == HttpMethods.Patch;

                if (isWriteBodyMethod && !IsFileUpload(request))
                {
                    requestBody = await ReadRequestBodyAsync(context);
                }

                // 🔹 Exception detail
                Exception currentEx = exception;
                int depth = 0;
                string fullDetail = "";

                while (currentEx != null)
                {
                    var line = currentEx.StackTrace?
                        .Split('\n')
                        .LastOrDefault(l => l.Contains(":line"))?
                        .Trim() ?? "No line info";

                    fullDetail += $"\n[{depth}] {currentEx.GetType().Name}: {currentEx.Message} | {line}";
                    currentEx = currentEx.InnerException;
                    depth++;
                }

                string message = exception.Message;

                // 🔔 Log / Telegram
                _ = Task.Run(() =>
                    Helper.SendTelegramMessageAsync(
                        $"❌ API: {apiInfo}\n" +
                        $"👤 UserId: {userId}\n" +
                        $"📦 BODY: {requestBody}\n" +
                        $"💥 Error: {message}\n" +
                        $"📌 Detail:{fullDetail}"
                    )
                );

                _logger.LogError(
                    $"❌ API: {apiInfo}\n" +
                    $"👤 UserId: {userId}\n" +
                    $"📦 BODY: {requestBody}\n" +
                    $"💥 Error: {message}\n" +
                    $"📌 Detail:{fullDetail}"
                );

                var response = new ApiResponse<object>(false, message);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;

                var json = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ExceptionMiddlewareError] {ex.Message}");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("{\"success\":false,\"message\":\"Middleware error\"}");
            }
        }
    }
}