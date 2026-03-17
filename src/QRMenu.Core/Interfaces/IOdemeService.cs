namespace QRMenu.Core.Interfaces
{
    public interface IOdemeService
    {
        Task<bool> ParcaliOdemeAsync(int masaId, List<int> siparisDetayIds, string odemeTipi);
    }
}
