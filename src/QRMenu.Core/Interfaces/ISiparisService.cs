using QRMenu.Core.Entities;
using QRMenu.Core.Enums;

namespace QRMenu.Core.Interfaces
{
    public interface ISiparisService
    {
        /// <summary>
        /// Sepetteki ürünleri siparişe çevirir (Transaction ile atomik).
        /// Sipariş oluştuktan sonra sepet temizlenir.
        /// </summary>
        Task<Siparis> SiparisOlusturAsync(int sepetId, string? notlar = null);

        /// <summary>
        /// Sipariş durumunu günceller (State Machine kurallarına uygun).
        /// Geçersiz geçişlerde InvalidOperationException fırlatır.
        /// RowVersion ile concurrency kontrolü yapar.
        /// </summary>
        Task<Siparis> DurumGuncelleAsync(int siparisId, SiparisDurum yeniDurum);

        /// <summary>
        /// Sipariş detayını getirir (SiparisDetaylar + Urun dahil)
        /// </summary>
        Task<Siparis?> GetSiparisAsync(int siparisId);

        /// <summary>
        /// Belirli bir masanın aktif siparişlerini listeler
        /// </summary>
        Task<List<Siparis>> GetSiparislerByMasaAsync(int masaId);

        /// <summary>
        /// Belirli bir oturumun siparişlerini listeler
        /// </summary>
        Task<List<Siparis>> GetSiparislerByOturumAsync(int oturumId);

        /// <summary>
        /// Siparişi iptal eder (uygun durumda ise)
        /// </summary>
        Task<Siparis> IptalEtAsync(int siparisId);

        /// <summary>
        /// Belirli bir durumdan geçiş yapılabilecek durumları döner
        /// </summary>
        IReadOnlyList<SiparisDurum> GecerliGecisler(SiparisDurum mevcutDurum);
    }
}
