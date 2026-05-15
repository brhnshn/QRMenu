using QRMenu.Core.Enums;

namespace QRMenu.Web.ViewModels;

public class GarsonTableCardViewModel
{
    public int MasaId { get; set; }
    public int MasaNo { get; set; }
    public bool SiparisVarMi { get; set; }
    public bool HesapBekliyorMu { get; set; }
    public bool HazirBekliyorMu { get; set; }
    public decimal ToplamTutar { get; set; }
    public int GecenDakika { get; set; }
}

public class GarsonMasalarPageViewModel
{
    public List<GarsonTableCardViewModel> Masalar { get; set; } = new();
    public int MusaitMasaSayisi { get; set; }
    public int DoluMasaSayisi { get; set; }
    public int HesapBekleyenMasaSayisi { get; set; }
    public int ToplamMasaSayisi { get; set; }
}

public class KasaBolgeChipViewModel
{
    public string Bolge { get; set; } = "Salon";
    public int Bekleyen { get; set; }
}

public class KasaTableCardViewModel
{
    public int MasaId { get; set; }
    public int MasaNo { get; set; }
    public string BolgeAd { get; set; } = "Salon";
    public bool DoluMu { get; set; }
    public string DurumMetni { get; set; } = "Hazir";
    public decimal KalanBakiye { get; set; }
    public int BeklemeDakika { get; set; }
    public bool AcilMi { get; set; }
    public bool OdemedeMi { get; set; }
    public bool MutfaktaMi { get; set; }
    public bool GarsondaMi { get; set; }
}

public class KasaMasalarPageViewModel
{
    public List<KasaTableCardViewModel> MasaKartlari { get; set; } = new();
    public List<KasaBolgeChipViewModel> BolgeChipleri { get; set; } = new();
    public int ToplamOdemeBekleyen { get; set; }
    public string? ConnectionWarning { get; set; }
}

public class GarsonSiparisDetaySummaryViewModel
{
    public int Adet { get; set; }
    public string UrunAd { get; set; } = string.Empty;
}

public class GarsonSiparisSummaryViewModel
{
    public int Id { get; set; }
    public int GunlukSiparisNo { get; set; }
    public SiparisDurum Durum { get; set; }
    public decimal ToplamTutar { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public string? MusteriNotu { get; set; }
    public List<GarsonSiparisDetaySummaryViewModel> Detaylar { get; set; } = new();
}

public class GarsonMasaPanelViewModel
{
    public int MasaId { get; set; }
    public int MasaNo { get; set; }
    public decimal ToplamAktifTutar { get; set; }
    public int HazirSiparisSayisi { get; set; }
    public string MasaDurumYazi { get; set; } = "Bos";
    public string MasaDurumSinif { get; set; } = "text-emerald-700";
    public string MasaDurumDot { get; set; } = "bg-emerald-600";
    public List<GarsonSiparisSummaryViewModel> AktifSiparisler { get; set; } = new();
    public List<GarsonSiparisSummaryViewModel> GecmisSiparisler { get; set; } = new();
    public List<GarsonSiparisSummaryViewModel> NotluSiparisler { get; set; } = new();
}

public class GarsonMasaPageViewModel
{
    public int MasaId { get; set; }
    public int MasaNo { get; set; }
    public GarsonMasaPanelViewModel Panel { get; set; } = new();
}
