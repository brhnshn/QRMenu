using System;
using System.Collections.Generic;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;

namespace QRMenu.Core.DTOs
{
    public class SaatlikCiroRow
    {
        public int Saat { get; set; }
        public decimal Ciro { get; set; }
    }

    public class EnCokSatanItemDto
    {
        public string Ad { get; set; } = string.Empty;
        public int Adet { get; set; }
        public string? GorselUrl { get; set; }
        public decimal Fiyat { get; set; }
    }

    public class ZOdemeTipiItemDto
    {
        public string Tip { get; set; } = string.Empty;
        public decimal Tutar { get; set; }
        public int Adet { get; set; }
    }

    public class GunSonuExportDto
    {
        public DateTime Tarih { get; set; }
        public bool KapaliMi { get; set; }
        public DateTime? KapanisTarihi { get; set; }
        public decimal ToplamCiro { get; set; }
        public int SiparisSayisi { get; set; }
        public List<ZOdemeTipiItemDto> OdemeTipleri { get; set; } = new();
        public List<GunSonuExportSiparisDto> Siparisler { get; set; } = new();
    }

    public class GunSonuExportSiparisDto
    {
        public int Id { get; set; }
        public int? MasaNo { get; set; }
        public string Durum { get; set; } = string.Empty;
        public string Saat { get; set; } = string.Empty;
        public decimal ToplamTutar { get; set; }
        public string? Notlar { get; set; }
        public List<GunSonuExportDetayDto> Urunler { get; set; } = new();
    }

    public class GunSonuExportDetayDto
    {
        public string UrunAd { get; set; } = string.Empty;
        public int Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public string? Secenekler { get; set; }
    }

    public class DashboardStatsDto
    {
        public decimal BugunCiro { get; set; }
        public decimal DunCiro { get; set; }
        public double CiroDegisim { get; set; }
        public int AktifMasalarCount { get; set; }
        public int ToplamMasaCount { get; set; }
        public int DolulukOrani { get; set; }
        public double OrtServisSuresi { get; set; }
        public List<SaatlikCiroRow> SaatlikData { get; set; } = new();
        public List<EnCokSatanItemDto> EnCokSatanlar { get; set; } = new();
        public List<EnCokSatanItemDto> BugunEnCokSatanlarTum { get; set; } = new();
        public List<Siparis> CanliSiparisler { get; set; } = new();
        public int PersonelCount { get; set; }
        public DateTime Baslangic { get; set; }
        public DateTime Bitis { get; set; }
        public DateTime ZRaporTarihi { get; set; }
        public decimal ZToplamCiro { get; set; }
        public int ZSiparisSayisi { get; set; }
        public List<ZOdemeTipiItemDto> ZOdemeTipleri { get; set; } = new();
        public bool GunKapaliMi { get; set; }
        public DateTime? GunKapanisTarihi { get; set; }
        public string? GunKapanisMetni { get; set; }
        public string ZNextOpeningIso { get; set; } = string.Empty;
    }

    public class LogListItemDto
    {
        public int Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string TimestampIso { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? User { get; set; }
        public string? Meta { get; set; }
    }

    public class SiparisArsivOzetDto
    {
        public int BugunToplamSayi { get; set; }
        public decimal BugunCiro { get; set; }
        public int IptalIadeSayi { get; set; }
        public decimal IptalIadeCiro { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public List<Siparis> Siparisler { get; set; } = new();
    }

    public class KullaniciOzetDto
    {
        public string Id { get; set; } = string.Empty;
        public string KullaniciAdi { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool AktifMi { get; set; }
    }

    public class HappyHourBilgiDto
    {
        public int Id { get; set; }
        public bool AktifMi { get; set; }
        public bool SuAnAktif { get; set; }
        public decimal IndirimOrani { get; set; }
        public string BaslangicSaati { get; set; } = string.Empty;
        public string BitisSaati { get; set; } = string.Empty;
        public int[] UrunIds { get; set; } = Array.Empty<int>();
    }

    public class HappyHourKayitDto
    {
        public string? BaslangicSaati { get; set; }
        public string? BitisSaati { get; set; }
        public decimal IndirimOrani { get; set; }
        public bool AktifMi { get; set; }
        public List<int>? UrunIds { get; set; }
    }

    public class MasaDetayDto
    {
        public int Id { get; set; }
        public int MasaNo { get; set; }
        public string QrUrl { get; set; } = string.Empty;
        public string QrBase64 { get; set; } = string.Empty;
        public bool DoluMu { get; set; }
        public int? BolgeId { get; set; }
        public string? BolgeAd { get; set; }
    }

    public class UrunStokGuncelleSonucDto
    {
        public int StokAdet { get; set; }
        public bool AktifMi { get; set; }
        public bool AdminManuelPasifMi { get; set; }
    }

    public class SiparisListItemDto
    {
        public int Id { get; set; }
        public int? MasaNo { get; set; }
        public string Durum { get; set; } = string.Empty;
        public int DurumInt { get; set; }
        public decimal ToplamTutar { get; set; }
        public string OlusturmaTarihi { get; set; } = string.Empty;
        public int UrunSayisi { get; set; }
        public string DetayOzet { get; set; } = string.Empty;
    }
}
