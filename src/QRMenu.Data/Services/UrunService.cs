using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Interfaces;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class UrunService : IUrunService
    {
        private readonly QRMenuDbContext _context;

        public UrunService(QRMenuDbContext context)
        {
            _context = context;
        }

        public async Task<List<Urun>> GetAllAsync()
        {
            return await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .OrderBy(u => u.Kategori.SiraNo)
                .ThenBy(u => u.Ad)
                .ToListAsync();
        }

        public async Task<List<Urun>> GetAktifAsync()
        {
            return await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .Where(u => u.AktifMi)
                .OrderBy(u => u.Kategori.SiraNo)
                .ThenBy(u => u.Ad)
                .ToListAsync();
        }

        public async Task<Urun?> GetByIdAsync(int id)
        {
            return await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<Urun> CreateAsync(Urun urun)
        {
            _context.Urunler.Add(urun);
            await _context.SaveChangesAsync();
            return urun;
        }

        public async Task<Urun> UpdateAsync(Urun urun)
        {
            _context.Urunler.Update(urun);
            await _context.SaveChangesAsync();
            return urun;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null) return false;

            _context.Urunler.Remove(urun);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
