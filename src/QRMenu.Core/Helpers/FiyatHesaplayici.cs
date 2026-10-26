using System;

namespace QRMenu.Core.Helpers
{
    public static class FiyatHesaplayici
    {
        /// <summary>
        /// Karar: Happy Hour indirimi, varyasyon ve opsiyonlar dahil toplam birim fiyata uygulanır.
        /// </summary>
        public static decimal BirimFiyatHesapla(
            decimal urunFiyat,
            decimal varyasyonEkFiyat,
            decimal opsiyonlarEkFiyat,
            decimal hhIndirimOrani,
            bool hhUygulanabilir)
        {
            var tabanFiyat = urunFiyat + varyasyonEkFiyat + opsiyonlarEkFiyat;
            if (hhUygulanabilir && hhIndirimOrani > 0)
            {
                return Math.Round(tabanFiyat * (1 - hhIndirimOrani / 100m), 2, MidpointRounding.AwayFromZero);
            }
            return tabanFiyat;
        }
    }
}
