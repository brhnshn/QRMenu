using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using System.Net;
using System.Net.Http.Json;

namespace QRMenu.IntegrationTests
{
    public class PaymentIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public PaymentIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task KasaTahsilat_UnauthorizedRole_ReturnsForbidden()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Garson");

            var payload = new
            {
                MasaId = 1,
                SiparisDetayIds = new List<int> { 1 },
                OdemeTipi = OdemeTipi.Nakit
            };

            // Act
            var response = await client.PostAsJsonAsync("/Kasa/TahsilatYap", payload);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Mutfak_CannotExecute_KasaTahsilat()
        {
            // Arrange
            var client = _factory.CreateClientWithRole("Mutfak");

            var payload = new
            {
                MasaId = 1,
                SiparisDetayIds = new List<int> { 1 },
                OdemeTipi = OdemeTipi.Kart
            };

            // Act
            var response = await client.PostAsJsonAsync("/Kasa/TahsilatYap", payload);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
