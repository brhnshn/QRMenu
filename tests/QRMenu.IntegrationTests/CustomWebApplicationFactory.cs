using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QRMenu.Data.Data;

namespace QRMenu.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "IntegrationTestDb_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=postgres",
                    ["Database:MaxPoolSize"] = "10"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<QRMenuDbContext>) ||
                    d.ServiceType == typeof(QRMenuDbContext)).ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database
                services.AddDbContext<QRMenuDbContext>((sp, options) =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });

                // Add Test Authentication
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });

                // Disable antiforgery token validation for testing
                services.PostConfigure<MvcOptions>(options =>
                {
                    var antiforgeryFilter = options.Filters.FirstOrDefault(f => f is AutoValidateAntiforgeryTokenAttribute);
                    if (antiforgeryFilter != null)
                    {
                        options.Filters.Remove(antiforgeryFilter);
                    }
                });
            });
        }

        public async Task ExecuteDbContextAsync(Func<QRMenuDbContext, Task> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();
            await action(db);
        }

        public HttpClient CreateClientWithRole(string? role = null)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(role))
            {
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
            }

            return client;
        }
    }
}
