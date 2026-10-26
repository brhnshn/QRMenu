using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface IUrunYonetimService
    {
        Task<List<Kategori>> GetKategorilerWithUrunlerAsync();
        Task<List<Kategori>> GetKategorilerAsync();
        Task<Kategori> KategoriEkleAsync(string ad, string? adEN, int siraNo);
        Task<bool> KategoriGuncelleAsync(int id, string ad, string? adEN, int siraNo);
        Task<(bool Success, string? ErrorMessage)> KategoriSilAsync(int id);
        Task<(bool Success, string? ErrorMessage, int Count)> UrunTasiAsync(List<int> urunIds, int yeniKategoriId);
        Task<Urun?> GetUrunDetayAsync(int id);
        Task<Urun> UrunEkleAsync(Urun urun, int stokAdet, bool aktifMi);
        Task<(bool Success, string? ErrorMessage)> UrunGuncelleAsync(int id, Action<Urun> updateAction, int stokAdet, bool aktifMi);
        Task<(bool Success, string? Message)> UrunSilAsync(int id);
        Task<(bool Success, string? ErrorMessage, bool AktifMi)> UrunToggleAsync(int id);
        Task<(bool Success, string? ErrorMessage, UrunStokGuncelleSonucDto? Sonuc)> UrunStokGuncelleAsync(int id, int stokAdet);
        Task<(bool Success, string? ErrorMessage, int OpsiyonId)> OpsiyonEkleAsync(int urunId, string ad, string? adEN, string grup, string? grupEN, decimal ekFiyat, bool zorunlu);
        Task<(bool Success, string? ErrorMessage)> OpsiyonSilAsync(int urunId, int opsiyonId);
        Task<List<(string Grup, string? GrupEN)>> GetOpsiyonGruplariAsync();
        Task<(byte[]? Data, string? ContentType)> GetUrunGorselFromDbAsync(int id);
        Task SaveUrunGorselPathAsync(int urunId, string gorselUrl);
    }
}
