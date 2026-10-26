using System.Net;

namespace QRMenu.IntegrationTests
{
    public class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public SecurityHeadersTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Response_ContainsRequiredSecurityHeaders()
        {
            // Arrange
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.True(response.Headers.Contains("X-Frame-Options"));
            Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").First());

            Assert.True(response.Headers.Contains("X-Content-Type-Options"));
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());

            Assert.True(response.Headers.Contains("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());

            Assert.True(response.Headers.Contains("Permissions-Policy"));
            Assert.True(response.Headers.Contains("Content-Security-Policy"));

            var csp = response.Headers.GetValues("Content-Security-Policy").First();
            Assert.Contains("default-src 'self'", csp);
            Assert.Contains("script-src", csp);
        }

        [Theory]
        [InlineData("/js/pages/mutfak-panel.js")]
        [InlineData("/js/pages/kasa-odeme.js")]
        [InlineData("/js/pages/admin-urunler.js")]
        [InlineData("/js/pages/menu-index.js")]
        [InlineData("/js/pages/siparis-siparislerim.js")]
        [InlineData("/js/tailwind-config.js")]
        public async Task ExternalPageScripts_ExistAndReturnOk(string scriptUrl)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(scriptUrl);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(content));
        }
    }
}
