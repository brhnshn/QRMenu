using System.Collections.Generic;
using System.Threading.Tasks;
using QRMenu.Core.DTOs;
using QRMenu.Core.Enums;

namespace QRMenu.Core.Interfaces
{
    public interface IKullaniciYonetimService
    {
        Task<List<KullaniciOzetDto>> GetKullaniciListesiAsync();
        Task<(bool Success, string? ErrorMessage, string? KullaniciId)> KullaniciEkleAsync(string userName, string adSoyad, string sifre, KullaniciRol rol);
        Task<(bool Success, string? ErrorMessage)> KullaniciGuncelleAsync(string id, string userName, string adSoyad, KullaniciRol rol, bool aktifMi);
        Task<(bool Success, string? ErrorMessage)> KullaniciSifreDegistirAsync(string id, string yeniSifre);
        Task<(bool Success, string? ErrorMessage)> KullaniciSilAsync(string id, string? currentUserId);
        Task<(bool Success, string? ErrorMessage, bool AktifMi)> KullaniciToggleAsync(string id);
    }
}
