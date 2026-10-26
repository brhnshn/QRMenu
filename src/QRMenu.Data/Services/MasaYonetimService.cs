using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Helpers;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class MasaYonetimService : IMasaYonetimService
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        private static DateTime RaporTarihi(DateTime utc) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).Date, DateTimeKind.Utc);

        private readonly QRMenuDbContext _context;
        private readonly ILogger<MasaYonetimService> _logger;

        public MasaYonetimService(QRMenuDbContext context, ILogger<MasaYonetimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Masa>> GetMasalarWithDetailsAsync()
        {
            return await _context.Masalar
                .Where(m => m.AktifMi)
                .Include(m => m.Bolge)
                .Include(m => m.Siparisler.Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.TamOdendi && s.Durum != SiparisDurum.Iade))
                .OrderBy(m => m.BolgeId.HasValue ? m.Bolge!.SiraNo : 9999)
                .ThenBy(m => m.MasaNo)
                .ToListAsync();
        }

        public async Task<List<Bolge>> GetBolgelerAsync()
        {
            return await _context.Bolgeler.OrderBy(b => b.SiraNo).ToListAsync();
        }

        public async Task EnsureMasaQrTokensAsync(string baseUrl)
        {
            var masalar = await _context.Masalar.Where(m => m.AktifMi).ToListAsync();
            bool tokenGuncellendi = false;

            foreach (var m in masalar)
            {
                if (string.IsNullOrWhiteSpace(m.QrToken))
                {
                    m.QrToken = QrTokenGenerator.GenerateToken();
                    m.QrKodUrl = $"{baseUrl}/qr/t/{m.QrToken}";
                    tokenGuncellendi = true;
                }
                else if (string.IsNullOrWhiteSpace(m.QrKodUrl) || m.QrKodUrl.Contains($"/qr/{m.MasaNo}"))
                {
                    m.QrKodUrl = $"{baseUrl}/qr/t/{m.QrToken}";
                    tokenGuncellendi = true;
                }
            }

            if (tokenGuncellendi)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<MasaDetayDto>> GetMasaDetayListAsync(string baseUrl)
        {
            await EnsureMasaQrTokensAsync(baseUrl);

            var masalar = await GetMasalarWithDetailsAsync();

            using var gen = new QRCodeGenerator();
            return masalar.Select(m =>
            {
                string base64;
                if (string.IsNullOrEmpty(m.QrKodUrl))
                {
                    base64 = string.Empty;
                }
                else
                {
                    var data = gen.CreateQrCode(m.QrKodUrl, QRCodeGenerator.ECCLevel.H);
                    using var qr = new PngByteQRCode(data);
                    base64 = Convert.ToBase64String(qr.GetGraphic(10));
                }

                return new MasaDetayDto
                {
                    Id = m.Id,
                    MasaNo = m.MasaNo,
                    QrUrl = m.QrKodUrl ?? "",
                    QrBase64 = base64,
                    DoluMu = m.Siparisler.Any(),
                    BolgeId = m.BolgeId,
                    BolgeAd = m.Bolge?.Ad
                };
            }).ToList();
        }

        public async Task<(bool Success, string? Message)> MasaOlusturAsync(int masaNo)
        {
            var mevcut = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                {
                    mevcut.AktifMi = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Masa pasif durumdan aktif yapıldı. MasaNo={MasaNo}", masaNo);
                    return (true, null);
                }
                return (false, $"Masa {masaNo} zaten var");
            }

            _context.Masalar.Add(new Masa { MasaNo = masaNo, AktifMi = true });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa oluşturuldu. MasaNo={MasaNo}", masaNo);
            return (true, null);
        }

        public async Task<(bool Success, string? Message)> MasaEkleAsync(int masaNo, int? bolgeId)
        {
            var mevcut = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                {
                    mevcut.AktifMi = true;
                    mevcut.BolgeId = bolgeId;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Pasif masa aktif edildi. MasaNo={MasaNo} BolgeId={BolgeId}", masaNo, bolgeId);
                    return (true, null);
                }
                return (false, $"Masa {masaNo} zaten var");
            }

            var yeniMasa = new Masa { MasaNo = masaNo, BolgeId = bolgeId, AktifMi = true };
            _context.Masalar.Add(yeniMasa);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Masa oluşturuldu. MasaNo={MasaNo} BolgeId={BolgeId}", masaNo, bolgeId);
            return (true, null);
        }

        public async Task<(bool Success, string? Message)> MasaGuncelleAsync(int id, int masaNo, int? bolgeId)
        {
            var masa = await _context.Masalar.FindAsync(id);
            if (masa == null) return (false, "Masa bulunamadı");

            if (masa.MasaNo != masaNo && await _context.Masalar.AnyAsync(m => m.MasaNo == masaNo))
                return (false, "Bu numarada başka bir masa var!");

            masa.MasaNo = masaNo;
            masa.BolgeId = bolgeId;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<Bolge> BolgeEkleAsync(string ad, int siraNo)
        {
            var b = new Bolge { Ad = ad, SiraNo = siraNo };
            _context.Bolgeler.Add(b);
            await _context.SaveChangesAsync();
            return b;
        }

        public async Task<(bool Success, string? Message)> BolgeGuncelleAsync(int id, string ad, int siraNo)
        {
            var b = await _context.Bolgeler.FindAsync(id);
            if (b == null) return (false, "Bulunamadı");

            b.Ad = ad;
            b.SiraNo = siraNo;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Message)> BolgeSilAsync(int id)
        {
            var b = await _context.Bolgeler.FindAsync(id);
            if (b == null) return (false, "Bulunamadı");

            if (await _context.Masalar.AnyAsync(m => m.BolgeId == id))
                return (false, "Bu bölgeye bağlı masalar var. Önce masaları kaldırın/taşıyın.");

            _context.Bolgeler.Remove(b);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, bool HasRecords, string? Message, int? MasaId)> MasaSilAsync(int masaNo, bool force)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return (false, false, "Masa bulunamadı", null);

            var oturumVar = await _context.Oturumlar.AnyAsync(o => o.MasaId == masa.Id);
            var siparisVar = await _context.Siparisler.AnyAsync(s => s.MasaId == masa.Id);

            if ((oturumVar || siparisVar) && !force)
            {
                // Soft delete and warn the user
                masa.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Masa pasife alındı (bağlı kayıt var). MasaNo={MasaNo}", masaNo);
                return (true, true, null, masa.Id);
            }

            if (force)
            {
                var siparisler = await _context.Siparisler.Where(s => s.MasaId == masa.Id).ToListAsync();
                if (siparisler.Any())
                {
                    var kilitliRaporTarihleri = siparisler.Select(s => RaporTarihi(s.OlusturmaTarihi)).Distinct().ToList();
                    var kilitliKayitVar = await _context.GunSonuRaporlari.AnyAsync(r => kilitliRaporTarihleri.Contains(r.Tarih));
                    if (kilitliKayitVar)
                        return (false, false, "Bu masada kapatılmış gün sonu kayıtları var; bağlı siparişler silinemez.", null);

                    var sipIds = siparisler.Select(s => s.Id).ToList();
                    var detaylar = await _context.SiparisDetaylar.Where(sd => sipIds.Contains(sd.SiparisId)).ToListAsync();
                    if (detaylar.Any()) _context.SiparisDetaylar.RemoveRange(detaylar);
                    _context.Siparisler.RemoveRange(siparisler);
                }
                var oturumlar = await _context.Oturumlar.Where(o => o.MasaId == masa.Id).ToListAsync();
                if (oturumlar.Any()) _context.Oturumlar.RemoveRange(oturumlar);
            }

            _context.Masalar.Remove(masa);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa silindi. MasaNo={MasaNo}", masaNo);
            return (true, false, null, null);
        }

        public async Task<(bool Success, string? Message, string? QrBase64, string? QrUrl, string? QrToken)> QrOlusturAsync(int masaNo, string baseUrl)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return (false, "Masa bulunamadı", null, null, null);

            if (string.IsNullOrWhiteSpace(masa.QrToken))
            {
                masa.QrToken = QrTokenGenerator.GenerateToken();
            }

            var qrUrl = $"{baseUrl.TrimEnd('/')}/qr/t/{masa.QrToken}";

            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.H);
            using var qr = new PngByteQRCode(data);
            var bytes = qr.GetGraphic(10);

            masa.QrKodUrl = qrUrl;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR oluşturuldu. Masa={MasaNo} Url={Url}", masaNo, qrUrl);
            return (true, null, Convert.ToBase64String(bytes), qrUrl, masa.QrToken);
        }

        public async Task<(bool Success, string? Message, string? QrBase64, string? QrUrl, string? QrToken)> QrYenileAsync(int masaNo, string baseUrl)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return (false, "Masa bulunamadı", null, null, null);

            masa.QrToken = QrTokenGenerator.GenerateToken();
            var qrUrl = $"{baseUrl.TrimEnd('/')}/qr/t/{masa.QrToken}";

            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.H);
            using var qr = new PngByteQRCode(data);
            var bytes = qr.GetGraphic(10);

            masa.QrKodUrl = qrUrl;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR token yenilendi. Masa={MasaNo} Url={Url}", masaNo, qrUrl);
            return (true, null, Convert.ToBase64String(bytes), qrUrl, masa.QrToken);
        }

        public async Task<bool> QrSilAsync(int masaNo)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return false;

            masa.QrKodUrl = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR silindi. Masa={MasaNo}", masaNo);
            return true;
        }
    }
}
