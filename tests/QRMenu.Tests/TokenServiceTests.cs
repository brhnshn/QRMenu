using Microsoft.EntityFrameworkCore;
using QRMenu.Data.Data;
using QRMenu.Data.Services;

namespace QRMenu.Tests
{
    public class TokenServiceTests
    {
        private QRMenuDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<QRMenuDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var context = new QRMenuDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public void GenerateToken_Should_Return_64_Hex_Characters()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            // Act
            var token = service.GenerateToken();

            // Assert
            Assert.NotNull(token);
            Assert.Equal(64, token.Length); // 256-bit = 32 bytes = 64 hex chars
            Assert.True(token.All(c => "0123456789abcdef".Contains(c)), "Token should be lowercase hex");
        }

        [Fact]
        public void GenerateToken_Should_Be_Unique()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            // Act
            var token1 = service.GenerateToken();
            var token2 = service.GenerateToken();

            // Assert
            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void HashToken_Should_Return_Consistent_SHA256()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);
            var token = "test_token_value";

            // Act
            var hash1 = service.HashToken(token);
            var hash2 = service.HashToken(token);

            // Assert
            Assert.Equal(hash1, hash2); // Aynı token → aynı hash
            Assert.Equal(64, hash1.Length); // SHA256 = 64 hex chars
        }

        [Fact]
        public void HashToken_Different_Tokens_Should_Produce_Different_Hashes()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            // Act
            var hash1 = service.HashToken("token1");
            var hash2 = service.HashToken("token2");

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public async Task CreateSessionAsync_Should_Create_Active_Session()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            // Seed: masa ekle
            context.Masalar.Add(new QRMenu.Core.Entities.Masa { Id = 100, MasaNo = 100, AktifMi = true });
            await context.SaveChangesAsync();

            // Act
            var (oturum, rawToken) = await service.CreateSessionAsync(100);

            // Assert
            Assert.NotNull(oturum);
            Assert.Equal(100, oturum.MasaId);
            Assert.True(oturum.AktifMi);
            Assert.False(oturum.Kullanildi);
            Assert.NotNull(rawToken);
            Assert.Equal(64, rawToken.Length); // 256-bit hex
        }

        [Fact]
        public async Task ValidateTokenAsync_Should_Return_Session_For_Valid_Token()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            context.Masalar.Add(new QRMenu.Core.Entities.Masa { Id = 101, MasaNo = 101, AktifMi = true });
            await context.SaveChangesAsync();

            // Token oluştur — CreateSessionAsync raw token döner
            var rawToken = service.GenerateToken();
            var tokenHash = service.HashToken(rawToken);

            var oturum = new QRMenu.Core.Entities.Oturum
            {
                MasaId = 101,
                TokenHash = tokenHash,
                Kullanildi = false,
                OlusturmaTarihi = DateTime.UtcNow,
                SonIslemTarihi = DateTime.UtcNow,
                AktifMi = true
            };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            // Act
            var result = await service.ValidateTokenAsync(tokenHash);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(101, result!.MasaId);
        }

        [Fact]
        public async Task ValidateTokenAsync_Should_Return_Null_For_Expired_Session()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            context.Masalar.Add(new QRMenu.Core.Entities.Masa { Id = 102, MasaNo = 102, AktifMi = true });
            await context.SaveChangesAsync();

            var tokenHash = service.HashToken("expired_token");
            var oturum = new QRMenu.Core.Entities.Oturum
            {
                MasaId = 102,
                TokenHash = tokenHash,
                Kullanildi = false,
                OlusturmaTarihi = DateTime.UtcNow.AddHours(-3),
                SonIslemTarihi = DateTime.UtcNow.AddMinutes(-100), // 90 dk'yı geçmiş
                AktifMi = true
            };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            // Act
            var result = await service.ValidateTokenAsync(tokenHash);

            // Assert
            Assert.Null(result); // Süresi dolmuş, null dönmeli
        }

        [Fact]
        public async Task ValidateTokenAsync_Should_Return_Null_For_Inactive_Session()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            context.Masalar.Add(new QRMenu.Core.Entities.Masa { Id = 103, MasaNo = 103, AktifMi = true });
            await context.SaveChangesAsync();

            var tokenHash = service.HashToken("inactive_token");
            var oturum = new QRMenu.Core.Entities.Oturum
            {
                MasaId = 103,
                TokenHash = tokenHash,
                Kullanildi = false,
                OlusturmaTarihi = DateTime.UtcNow,
                SonIslemTarihi = DateTime.UtcNow,
                AktifMi = false // Pasif oturum
            };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            // Act
            var result = await service.ValidateTokenAsync(tokenHash);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CleanExpiredSessionsAsync_Should_Deactivate_Old_Sessions()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new TokenService(context);

            context.Masalar.Add(new QRMenu.Core.Entities.Masa { Id = 104, MasaNo = 104, AktifMi = true });
            await context.SaveChangesAsync();

            // Süresi dolmuş oturum
            context.Oturumlar.Add(new QRMenu.Core.Entities.Oturum
            {
                MasaId = 104,
                TokenHash = "expired_hash_1",
                SonIslemTarihi = DateTime.UtcNow.AddMinutes(-120),
                AktifMi = true
            });

            // Aktif oturum
            context.Oturumlar.Add(new QRMenu.Core.Entities.Oturum
            {
                MasaId = 104,
                TokenHash = "active_hash_1",
                SonIslemTarihi = DateTime.UtcNow,
                AktifMi = true
            });
            await context.SaveChangesAsync();

            // Act
            await service.CleanExpiredSessionsAsync();

            // Assert
            var allSessions = context.Oturumlar.ToList();
            Assert.Single(allSessions.Where(s => s.AktifMi)); // Sadece 1 aktif kalmalı
            Assert.Single(allSessions.Where(s => !s.AktifMi)); // 1 pasife düşmeli
        }
    }
}
