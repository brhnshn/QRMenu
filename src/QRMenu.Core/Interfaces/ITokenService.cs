using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface ITokenService
    {
        /// <summary>
        /// 256-bit CSPRNG token üretir (hex string olarak döner)
        /// </summary>
        string GenerateToken();

        /// <summary>
        /// Token'ı SHA256 ile hash'ler
        /// </summary>
        string HashToken(string token);

        /// <summary>
        /// Yeni oturum oluşturur, DB'ye kaydeder, (Oturum, RawToken) döner
        /// </summary>
        Task<(Oturum oturum, string rawToken)> CreateSessionAsync(int masaId);

        /// <summary>
        /// Token hash'i ile oturumu doğrular (aktiflik, süre, tek kullanım kontrolü)
        /// </summary>
        Task<Oturum?> ValidateTokenAsync(string tokenHash);

        /// <summary>
        /// Oturumun SonIslemTarihi'ni günceller (Sliding Expiration)
        /// </summary>
        Task RefreshSessionAsync(int oturumId);

        /// <summary>
        /// Süresi dolan oturumları pasife çeker
        /// </summary>
        Task CleanExpiredSessionsAsync();
    }
}
