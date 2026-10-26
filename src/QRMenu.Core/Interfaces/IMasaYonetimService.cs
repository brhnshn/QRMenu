using System.Collections.Generic;
using System.Threading.Tasks;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface IMasaYonetimService
    {
        Task<List<Masa>> GetMasalarWithDetailsAsync();
        Task<List<Bolge>> GetBolgelerAsync();
        Task EnsureMasaQrTokensAsync(string baseUrl);
        Task<List<MasaDetayDto>> GetMasaDetayListAsync(string baseUrl);
        Task<(bool Success, string? Message)> MasaOlusturAsync(int masaNo);
        Task<(bool Success, string? Message)> MasaEkleAsync(int masaNo, int? bolgeId);
        Task<(bool Success, string? Message)> MasaGuncelleAsync(int id, int masaNo, int? bolgeId);
        Task<Bolge> BolgeEkleAsync(string ad, int siraNo);
        Task<(bool Success, string? Message)> BolgeGuncelleAsync(int id, string ad, int siraNo);
        Task<(bool Success, string? Message)> BolgeSilAsync(int id);
        Task<(bool Success, bool HasRecords, string? Message, int? MasaId)> MasaSilAsync(int masaNo, bool force);
        Task<(bool Success, string? Message, string? QrBase64, string? QrUrl, string? QrToken)> QrOlusturAsync(int masaNo, string baseUrl);
        Task<(bool Success, string? Message, string? QrBase64, string? QrUrl, string? QrToken)> QrYenileAsync(int masaNo, string baseUrl);
        Task<bool> QrSilAsync(int masaNo);
    }
}
