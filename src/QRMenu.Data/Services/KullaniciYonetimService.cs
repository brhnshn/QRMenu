using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;

namespace QRMenu.Data.Services
{
    public class KullaniciYonetimService : IKullaniciYonetimService
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly ILogger<KullaniciYonetimService> _logger;

        public KullaniciYonetimService(UserManager<Kullanici> userManager, ILogger<KullaniciYonetimService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<List<KullaniciOzetDto>> GetKullaniciListesiAsync()
        {
            var kullanicilar = await _userManager.Users
                .OrderBy(k => k.Rol)
                .ThenBy(k => k.UserName)
                .Select(k => new KullaniciOzetDto
                {
                    Id = k.Id,
                    KullaniciAdi = k.UserName ?? string.Empty,
                    AdSoyad = k.AdSoyad,
                    Rol = k.Rol.ToString(),
                    AktifMi = k.AktifMi
                })
                .ToListAsync();

            return kullanicilar;
        }

        public async Task<(bool Success, string? ErrorMessage, string? KullaniciId)> KullaniciEkleAsync(string userName, string adSoyad, string sifre, KullaniciRol rol)
        {
            var kullanici = new Kullanici
            {
                UserName = userName,
                AdSoyad = adSoyad,
                Rol = rol,
                AktifMi = true
            };

            var result = await _userManager.CreateAsync(kullanici, sifre);
            if (!result.Succeeded)
            {
                var hatalar = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, hatalar, null);
            }

            await _userManager.AddToRoleAsync(kullanici, rol.ToString());
            _logger.LogInformation("Kullanıcı eklendi. Id={Id}, UserName={Ad}, Rol={Rol}",
                kullanici.Id, kullanici.UserName, kullanici.Rol);

            return (true, null, kullanici.Id);
        }

        public async Task<(bool Success, string? ErrorMessage)> KullaniciGuncelleAsync(string id, string userName, string adSoyad, KullaniciRol rol, bool aktifMi)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı.");

            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return (false, "Güvenlik: Admin hesapları bu ekrandan güncellenemez.");
            }

            if (kullanici.Rol != rol)
            {
                var eskiRoller = await _userManager.GetRolesAsync(kullanici);
                await _userManager.RemoveFromRolesAsync(kullanici, eskiRoller);
                await _userManager.AddToRoleAsync(kullanici, rol.ToString());
            }

            kullanici.UserName = userName;
            kullanici.AdSoyad = adSoyad;
            kullanici.Rol = rol;
            kullanici.AktifMi = aktifMi;

            var updateResult = await _userManager.UpdateAsync(kullanici);
            if (!updateResult.Succeeded)
            {
                var hatalar = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return (false, hatalar);
            }

            await _userManager.UpdateSecurityStampAsync(kullanici);
            _logger.LogInformation("Kullanıcı güncellendi. Id={Id}, Rol={Rol}, Aktif={Aktif}", id, rol, aktifMi);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> KullaniciSifreDegistirAsync(string id, string yeniSifre)
        {
            if (string.IsNullOrWhiteSpace(yeniSifre) || yeniSifre.Length < 6)
                return (false, "Şifre en az 6 karakter olmalıdır.");

            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(kullanici);
            var result = await _userManager.ResetPasswordAsync(kullanici, token, yeniSifre);

            if (!result.Succeeded)
            {
                var hatalar = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, hatalar);
            }

            _logger.LogInformation("Şifre değiştirildi. KullaniciId={Id}", id);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> KullaniciSilAsync(string id, string? currentUserId)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı.");

            if (currentUserId == id)
                return (false, "Kendinizi silemezsiniz.");

            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return (false, "Güvenlik: Admin hesapları silinemez.");
            }

            var result = await _userManager.DeleteAsync(kullanici);
            if (!result.Succeeded)
                return (false, "Silme işlemi başarısız.");

            _logger.LogInformation("Kullanıcı silindi. Id={Id}, UserName={Ad}", id, kullanici.UserName);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage, bool AktifMi)> KullaniciToggleAsync(string id)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı.", false);

            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return (false, "Güvenlik: Admin hesaplarının durumu değiştirilemez.", false);
            }

            kullanici.AktifMi = !kullanici.AktifMi;
            await _userManager.UpdateAsync(kullanici);
            await _userManager.UpdateSecurityStampAsync(kullanici);

            _logger.LogInformation("Kullanıcı durumu değiştirildi. Id={Id}, AktifMi={AktifMi}", id, kullanici.AktifMi);
            return (true, null, kullanici.AktifMi);
        }
    }
}
