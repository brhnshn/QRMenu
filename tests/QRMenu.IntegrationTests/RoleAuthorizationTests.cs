using System.Net;

namespace QRMenu.IntegrationTests
{
    public class RoleAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public RoleAuthorizationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/Admin")]
        [InlineData("/Admin/Urunler")]
        [InlineData("/Admin/Masalar")]
        [InlineData("/Admin/Kullanicilar")]
        [InlineData("/Kasa/Masalar")]
        [InlineData("/Mutfak/Panel")]
        [InlineData("/Garson/Masalar")]
        public async Task Anonymous_AccessingProtectedEndpoints_RedirectsOrDenies(string url)
        {
            // Arrange
            var client = _factory.CreateClientWithRole(null);

            // Act
            var response = await client.GetAsync(url);

            // Assert: Redirect to Login or 401
            Assert.True(
                response.StatusCode == HttpStatusCode.Redirect || 
                response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Found);

            if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Found)
            {
                var location = response.Headers.Location?.ToString() ?? "";
                Assert.Contains("Login", location, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData("/Admin")]
        [InlineData("/Admin/Urunler")]
        [InlineData("/Admin/Kullanicilar")]
        [InlineData("/Kasa/Masalar")]
        public async Task MutfakRole_AccessingUnauthorizedEndpoints_ReturnsForbidden(string url)
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Mutfak");

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Theory]
        [InlineData("/Admin")]
        [InlineData("/Admin/Kullanicilar")]
        [InlineData("/Kasa/Masalar")]
        public async Task GarsonRole_AccessingAdminOrKasa_ReturnsForbidden(string url)
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Garson");

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Theory]
        [InlineData("/Admin")]
        [InlineData("/Admin/Kullanicilar")]
        public async Task KasaRole_AccessingAdmin_ReturnsForbidden(string url)
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Kasa");

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AdminRole_CanAccessAdminDashboard()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Admin");

            // Act
            var response = await client.GetAsync("/Admin/Panel");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AdminRole_AccessingAdminRoot_RedirectsToPanel()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Admin");

            // Act
            var response = await client.GetAsync("/Admin");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location?.ToString() ?? "";
            Assert.Contains("/admin/panel", location, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MutfakRole_CanAccessMutfakPanel()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Mutfak");

            // Act
            var response = await client.GetAsync("/Mutfak/Panel");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task KasaRole_CanAccessKasaMasalar()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Kasa");

            // Act
            var response = await client.GetAsync("/Kasa/Masalar");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GarsonRole_CanAccessGarsonMasalar()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Garson");

            // Act
            var response = await client.GetAsync("/Garson/Masalar");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("/Admin/Urunler")]
        [InlineData("/Admin/Masalar")]
        [InlineData("/Admin/Kullanicilar")]
        [InlineData("/admin/happy-hour")]
        [InlineData("/Admin/HappyHour")]
        [InlineData("/Admin/Siparisler")]
        [InlineData("/Admin/Loglar")]
        public async Task AdminRole_CanAccessAllModularAdminEndpoints(string url)
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Admin");

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
