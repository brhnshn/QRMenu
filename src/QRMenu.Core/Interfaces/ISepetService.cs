using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface ISepetService
    {
        /// <summary>
        /// Oturumun sepetini getir, yoksa yeni sepet oluştur
        /// </summary>
        Task<Sepet> GetOrCreateSepetAsync(int oturumId);

        /// <summary>
        /// Sepete ürün ekle (aynı ürün+opsiyon varsa adet artır)
        /// </summary>
        Task<SepetDetay> AddItemAsync(int sepetId, int urunId, int adet, List<int>? opsiyonIds = null);

        /// <summary>
        /// Sepetten ürün çıkar
        /// </summary>
        Task<bool> RemoveItemAsync(int sepetDetayId);

        /// <summary>
        /// Sepetteki ürünün adedini güncelle
        /// </summary>
        Task<bool> UpdateQuantityAsync(int sepetDetayId, int yeniAdet);

        /// <summary>
        /// Sepeti tamamen temizle
        /// </summary>
        Task ClearSepetAsync(int sepetId);

        /// <summary>
        /// Sepet özetini getir (detaylarla birlikte)
        /// </summary>
        Task<Sepet?> GetSepetWithDetailsAsync(int sepetId);

        /// <summary>
        /// Oturum ID'ye göre sepet özetini getir
        /// </summary>
        Task<Sepet?> GetSepetByOturumAsync(int oturumId);
    }
}
