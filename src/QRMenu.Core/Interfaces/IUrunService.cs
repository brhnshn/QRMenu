using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface IUrunService
    {
        Task<List<Urun>> GetAllAsync();
        Task<List<Urun>> GetAktifAsync();
        Task<Urun?> GetByIdAsync(int id);
        Task<Urun> CreateAsync(Urun urun);
        Task<Urun> UpdateAsync(Urun urun);
        Task<bool> DeleteAsync(int id);
    }
}
