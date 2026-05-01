using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QRMenu.Web.Middleware
{
    public class SecurityLogMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityLogMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider, ILogger<SecurityLogMiddleware> logger)
        {
            await _next(context);

            var statusCode = context.Response.StatusCode;

            // Sadece güvenlik ile ilgili hataları logla (401, 403, 429)
            if (statusCode == 401 || statusCode == 403 || statusCode == 429)
            {
                var eventType = statusCode switch
                {
                    401 => "Unauthorized",
                    403 => "Forbidden",
                    429 => "RateLimit",
                    _ => "SecurityEvent"
                };

                var message = eventType switch
                {
                    "Unauthorized" => "Giriş yapılmadan korumalı alana erişim denemesi.",
                    "Forbidden" => "Yetkisiz alana erişim denemesi (Rol yetersiz).",
                    "RateLimit" => "Çok fazla istek gönderildi (Spam korumasına takıldı).",
                    _ => "Bilinmeyen güvenlik olayı."
                };

                var path = context.Request.Path.ToString();
                var method = context.Request.Method;
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                var userId = context.User?.Identity?.IsAuthenticated == true
                    ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity.Name
                    : null;

                logger.LogWarning("Security Event: {EventType} - {Message} | IP: {IP} | Path: {Method} {Path}",
                    eventType, message, ip, method, path);

                // Scope oluşturarak veritabanına kaydet
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();

                var severity = statusCode switch
                {
                    429 => "Warning",
                    403 => "Critical",
                    401 => "Warning",
                    _ => "Info"
                };

                // Masa ID veya Personel bilgisini çekmeye çalış
                var tableId = context.Session.GetString("MasaId") ?? context.Request.Cookies["MasaId"];

                var log = new SecurityLog
                {
                    EventType = eventType,
                    Message = message,
                    IpAddress = ip,
                    Path = path,
                    Method = method,
                    UserAgent = userAgent,
                    UserId = userId,
                    TableId = tableId,
                    Severity = severity,
                    CountryCode = ip == "::1" || ip == "127.0.0.1" ? "TR" : "UN", // Basit bir eşleme, ileride API eklenebilir
                    Timestamp = DateTime.UtcNow
                };

                dbContext.SecurityLogs.Add(log);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
