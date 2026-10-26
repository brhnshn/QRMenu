using System.Net;

namespace QRMenu.IntegrationTests
{
    public class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public RateLimitingTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task QrEndpoint_ExceedingRateLimit_Returns429TooManyRequests()
        {
            // Arrange
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // QrRateLimitPolicy allows 30 requests per minute
            var got429 = false;

            // Act: send 35 requests rapidly
            for (int i = 0; i < 35; i++)
            {
                var response = await client.GetAsync("/qr/t/test-rate-limit-token");
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    got429 = true;
                    break;
                }
            }

            // Assert
            Assert.True(got429, "Rate limiter should have returned 429 TooManyRequests when exceeding limit.");
        }
    }
}
