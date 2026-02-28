using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;

namespace QRMenu.Data.Data
{
    public class QRMenuDbContext : DbContext
    {
        public QRMenuDbContext(DbContextOptions<QRMenuDbContext> options) : base(options) { }

        // DbSet tanımları
        public DbSet<Masa> Masalar => Set<Masa>();
        public DbSet<Kategori> Kategoriler => Set<Kategori>();
        public DbSet<Urun> Urunler => Set<Urun>();
        public DbSet<Opsiyon> Opsiyonlar => Set<Opsiyon>();
        public DbSet<UrunOpsiyon> UrunOpsiyonlar => Set<UrunOpsiyon>();
        public DbSet<Oturum> Oturumlar => Set<Oturum>();
        public DbSet<Sepet> Sepetler => Set<Sepet>();
        public DbSet<SepetDetay> SepetDetaylar => Set<SepetDetay>();
        public DbSet<Siparis> Siparisler => Set<Siparis>();
        public DbSet<SiparisDetay> SiparisDetaylar => Set<SiparisDetay>();
        public DbSet<Odeme> Odemeler => Set<Odeme>();
        public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
        public DbSet<AuditLog> AuditLoglar => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== MASA =====
            modelBuilder.Entity<Masa>(entity =>
            {
                entity.ToTable("Masalar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MasaNo).IsRequired();
                entity.HasIndex(e => e.MasaNo).IsUnique();
            });

            // ===== KATEGORİ =====
            modelBuilder.Entity<Kategori>(entity =>
            {
                entity.ToTable("Kategoriler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AdEN).HasMaxLength(100);
            });

            // ===== ÜRÜN =====
            modelBuilder.Entity<Urun>(entity =>
            {
                entity.ToTable("Urunler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ad).IsRequired().HasMaxLength(200);
                entity.Property(e => e.AdEN).HasMaxLength(200);
                entity.Property(e => e.Aciklama).HasMaxLength(500);
                entity.Property(e => e.AciklamaEN).HasMaxLength(500);
                entity.Property(e => e.Fiyat).HasPrecision(18, 2);
                entity.Property(e => e.GorselUrl).HasMaxLength(500);

                // Index: Aktif ürünlerin hızlı sorgulanması
                entity.HasIndex(e => e.AktifMi).HasDatabaseName("IX_Urunler_AktifMi");

                // Foreign Key: Kategori
                entity.HasOne(e => e.Kategori)
                      .WithMany(k => k.Urunler)
                      .HasForeignKey(e => e.KategoriId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== OPSİYON =====
            modelBuilder.Entity<Opsiyon>(entity =>
            {
                entity.ToTable("Opsiyonlar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ad).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AdEN).HasMaxLength(100);
                entity.Property(e => e.Grup).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EkFiyat).HasPrecision(18, 2);
            });

            // ===== ÜRÜN-OPSİYON (M2M) =====
            modelBuilder.Entity<UrunOpsiyon>(entity =>
            {
                entity.ToTable("UrunOpsiyonlar");
                entity.HasKey(e => new { e.UrunId, e.OpsiyonId }); // Composite Key

                entity.HasOne(e => e.Urun)
                      .WithMany(u => u.UrunOpsiyonlar)
                      .HasForeignKey(e => e.UrunId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Opsiyon)
                      .WithMany(o => o.UrunOpsiyonlar)
                      .HasForeignKey(e => e.OpsiyonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== OTURUM =====
            modelBuilder.Entity<Oturum>(entity =>
            {
                entity.ToTable("Oturumlar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(64); // SHA256 = 64 hex chars

                // Index: Token hash ile hızlı arama
                entity.HasIndex(e => e.TokenHash).HasDatabaseName("IX_Oturumlar_TokenHash");
                entity.HasIndex(e => e.AktifMi).HasDatabaseName("IX_Oturumlar_AktifMi");

                // Foreign Key: Masa
                entity.HasOne(e => e.Masa)
                      .WithMany(m => m.Oturumlar)
                      .HasForeignKey(e => e.MasaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== SEPET =====
            modelBuilder.Entity<Sepet>(entity =>
            {
                entity.ToTable("Sepetler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ToplamTutar).HasPrecision(18, 2);

                // 1:1 Oturum-Sepet ilişkisi
                entity.HasOne(e => e.Oturum)
                      .WithOne(o => o.Sepet)
                      .HasForeignKey<Sepet>(e => e.OturumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== SEPET DETAY =====
            modelBuilder.Entity<SepetDetay>(entity =>
            {
                entity.ToTable("SepetDetaylar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BirimFiyat).HasPrecision(18, 2);
                entity.Property(e => e.SeciliOpsiyonlar).HasMaxLength(1000);

                entity.HasOne(e => e.Sepet)
                      .WithMany(s => s.SepetDetaylar)
                      .HasForeignKey(e => e.SepetId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Urun)
                      .WithMany()
                      .HasForeignKey(e => e.UrunId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== SİPARİŞ =====
            modelBuilder.Entity<Siparis>(entity =>
            {
                entity.ToTable("Siparisler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ToplamTutar).HasPrecision(18, 2);
                entity.Property(e => e.Notlar).HasMaxLength(500);

                // Concurrency: RowVersion
                entity.Property(e => e.RowVersion).IsRowVersion();

                // Indexes
                entity.HasIndex(e => e.Durum).HasDatabaseName("IX_Siparisler_Durum");
                entity.HasIndex(e => e.MasaId).HasDatabaseName("IX_Siparisler_MasaId");

                // Foreign Keys
                entity.HasOne(e => e.Masa)
                      .WithMany(m => m.Siparisler)
                      .HasForeignKey(e => e.MasaId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Oturum)
                      .WithMany()
                      .HasForeignKey(e => e.OturumId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== SİPARİŞ DETAY =====
            modelBuilder.Entity<SiparisDetay>(entity =>
            {
                entity.ToTable("SiparisDetaylar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BirimFiyat).HasPrecision(18, 2);
                entity.Property(e => e.SeciliOpsiyonlar).HasMaxLength(1000);

                entity.HasOne(e => e.Siparis)
                      .WithMany(s => s.SiparisDetaylar)
                      .HasForeignKey(e => e.SiparisId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Urun)
                      .WithMany()
                      .HasForeignKey(e => e.UrunId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== ÖDEME =====
            modelBuilder.Entity<Odeme>(entity =>
            {
                entity.ToTable("Odemeler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tutar).HasPrecision(18, 2);

                entity.HasOne(e => e.Siparis)
                      .WithMany(s => s.Odemeler)
                      .HasForeignKey(e => e.SiparisId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== KULLANICI =====
            modelBuilder.Entity<Kullanici>(entity =>
            {
                entity.ToTable("Kullanicilar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.KullaniciAdi).IsRequired().HasMaxLength(50);
                entity.Property(e => e.AdSoyad).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SifreHash).IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.KullaniciAdi).IsUnique();
            });

            // ===== AUDIT LOG =====
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLoglar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TabloAdi).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Islem).IsRequired().HasMaxLength(20);
                entity.Property(e => e.EskiDeger).HasColumnType("text");
                entity.Property(e => e.YeniDeger).HasColumnType("text");
            });

            // ===== SEED DATA =====
            SeedData.Seed(modelBuilder);
        }
    }
}
