using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;

namespace QRMenu.Core.Interfaces
{
    public interface IRaporYonetimService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync(DateTime? baslangic, DateTime? bitis);
        Task<(bool Success, string? Message)> GunSonuKapatAsync(string? tarih, string? userId);
        Task<(bool Success, string? Message)> GunSonuAcAsync(string? tarih);
        Task<GunSonuExportDto> BuildGunSonuExportModelAsync(string? tarih);
        byte[] CreateGunSonuExcel(GunSonuExportDto model);
        Task<List<Siparis>> BuildSiparislerPdfListAsync(string? tarih, string? durum, int? masaId, string? arama);
        Task<List<LogListItemDto>> GetLoglarDataAsync(string? search, string? area, string? range, int? take);
        Task<List<SecurityLog>> GetLoglarPdfQueryAsync(string search, string eventType, string range, string tarih);
        Task<SiparisArsivOzetDto> GetSiparisArsivOzetAsync(int page, int pageSize, int? masaId);
        Task<int?> GetMasaNoByMasaIdAsync(int masaId);
        Task<List<SiparisListItemDto>> GetSiparislerJsonListAsync(string? durum, int? masaId);
    }
}
