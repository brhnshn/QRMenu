using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using System.Net;

namespace QRMenu.IntegrationTests
{
    public class QrTokenRoutingTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public QrTokenRoutingTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TokenGiris_InvalidToken_ReturnsNotFound()
        {
            // Arrange
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/qr/t/invalid-token-xyz");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TokenGiris_ValidToken_RedirectsToMenuWithCookie()
        {
            // Arrange
            var validToken = "test-token-" + Guid.NewGuid().ToString("N");
            await _factory.ExecuteDbContextAsync(async db =>
            {
                db.Masalar.Add(new Masa
                {
                    MasaNo = 99,
                    AktifMi = true,
                    QrToken = validToken
                });
                await db.SaveChangesAsync();
            });

            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync($"/qr/t/{validToken}");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location?.ToString() ?? "";
            Assert.True(location == "/" || location.Contains("Menu"), $"Expected redirect to / or /Menu, got: {location}");

            // Should set qrmenu_token cookie
            Assert.True(response.Headers.Contains("Set-Cookie"));
            var cookies = string.Join("; ", response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("qrmenu_token", cookies);
        }

        [Fact]
        public async Task LegacyQr_NonDevEnvironment_ReturnsNotFound()
        {
            // Arrange: in Testing environment (non-development), legacy route /qr/{masaNo} returns 404
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/qr/1");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
