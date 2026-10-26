using System.Collections.Generic;
using System.Threading.Tasks;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface IHappyHourYonetimService
    {
        Task<(HappyHour HappyHour, List<Kategori> Kategoriler, List<Urun> Urunler, List<int> SeciliUrunIds)> GetHappyHourDataAsync();
        Task<HappyHourBilgiDto> GetHappyHourBilgiAsync();
        Task<(bool Success, string Message, List<int> EtkilenenUrunIds, HappyHour? HappyHour)> HappyHourKaydetAsync(HappyHourKayitDto model);
        Task UpdateSepetFiyatlariAsync(HappyHour? happyHour, List<int> etkilenenUrunIds);
    }
}
