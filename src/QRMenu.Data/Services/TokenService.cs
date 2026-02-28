using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Interfaces;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class TokenService : ITokenService
    {
        private readonly QRMenuDbContext _context;
        private const int TOKEN_EXPIRATION_MINUTES = 90; // Sliding expiration süresi

        public TokenService(QRMenuDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 256-bit (32 byte) CSPRNG token üretir ve hex string olarak döner
        /// </summary>
        public string GenerateToken()
        {
            byte[] tokenBytes = new byte[32]; // 256-bit
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToHexString(tokenBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Token'ı SHA256 ile hash'ler (DB'de hash saklanır, raw token saklanmaz)
        /// </summary>
        public string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Yeni oturum oluşturur: token üretir, hash'ler, DB'ye kaydeder
        /// Tuple olarak (Oturum, RawToken) döner
        /// </summary>
        public async Task<(Oturum oturum, string rawToken)> CreateSessionAsync(int masaId)
        {
            var rawToken = GenerateToken();
            var tokenHash = HashToken(rawToken);

            var oturum = new Oturum
            {
                MasaId = masaId,
                TokenHash = tokenHash,
                Kullanildi = false,
                OlusturmaTarihi = DateTime.UtcNow,
                SonIslemTarihi = DateTime.UtcNow,
                AktifMi = true
            };

            _context.Oturumlar.Add(oturum);
            await _context.SaveChangesAsync();

            return (oturum, rawToken);
        }

        /// <summary>
        /// Token doğrulama: hash'le, DB'den ara, aktiflik ve süre kontrolü yap
        /// </summary>
        public async Task<Oturum?> ValidateTokenAsync(string tokenHash)
        {
            var oturum = await _context.Oturumlar
                .Include(o => o.Masa)
                .FirstOrDefaultAsync(o => o.TokenHash == tokenHash && o.AktifMi);

            if (oturum == null)
                return null;

            // Sliding expiration kontrolü: son işlem + 90 dk
            if (DateTime.UtcNow - oturum.SonIslemTarihi > TimeSpan.FromMinutes(TOKEN_EXPIRATION_MINUTES))
            {
                oturum.AktifMi = false;
                await _context.SaveChangesAsync();
                return null;
            }

            return oturum;
        }

        /// <summary>
        /// Oturumun SonIslemTarihi'ni günceller (her geçerli istekte çağrılır)
        /// </summary>
        public async Task RefreshSessionAsync(int oturumId)
        {
            var oturum = await _context.Oturumlar.FindAsync(oturumId);
            if (oturum != null && oturum.AktifMi)
            {
                oturum.SonIslemTarihi = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Süresi dolan tüm oturumları pasife çeker (Background job tarafından çağrılır)
        /// </summary>
        public async Task CleanExpiredSessionsAsync()
        {
            var expirationThreshold = DateTime.UtcNow.AddMinutes(-TOKEN_EXPIRATION_MINUTES);

            var expiredSessions = await _context.Oturumlar
                .Where(o => o.AktifMi && o.SonIslemTarihi < expirationThreshold)
                .ToListAsync();

            foreach (var session in expiredSessions)
            {
                session.AktifMi = false;
            }

            if (expiredSessions.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
