using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;

namespace QRMenu.Data.Data
{
    public class QRMenuDbContext : IdentityDbContext<Kullanici>
    {
        public QRMenuDbContext(DbContextOptions<QRMenuDbContext> options) : base(options) { }

        // DbSet tanımları
        public DbSet<Bolge> Bolgeler => Set<Bolge>();
        public DbSet<Masa> Masalar => Set<Masa>();
        public DbSet<Kategori> Kategoriler => Set<Kategori>();
        public DbSet<Urun> Urunler => Set<Urun>();
        public DbSet<Opsiyon> Opsiyonlar => Set<Opsiyon>();
        public DbSet<UrunOpsiyon> UrunOpsiyonlar => Set<UrunOpsiyon>();
        public DbSet<UrunGorsel> UrunGorseller => Set<UrunGorsel>();
        public DbSet<Oturum> Oturumlar => Set<Oturum>();
        public DbSet<Sepet> Sepetler => Set<Sepet>();
        public DbSet<SepetDetay> SepetDetaylar => Set<SepetDetay>();
        public DbSet<Siparis> Siparisler => Set<Siparis>();
        public DbSet<SiparisDetay> SiparisDetaylar => Set<SiparisDetay>();
        public DbSet<Odeme> Odemeler => Set<Odeme>();        public DbSet<OyunAyar> OyunAyarlar => Set<OyunAyar>();
        public DbSet<OyunOdul> OyunOduller => Set<OyunOdul>();
        public DbSet<KazanilanIndirim> KazanilanIndirimler => Set<KazanilanIndirim>();        // Kullanicilar artık Identity üzerinden yönetilmektedir (AspNetUsers tablosu)
        public DbSet<AuditLog> AuditLoglar => Set<AuditLog>();
        public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
        public DbSet<HappyHour> HappyHourlar => Set<HappyHour>();
        public DbSet<HappyHourUrun> HappyHourUrunler => Set<HappyHourUrun>();
        public DbSet<GunSonuRapor> GunSonuRaporlari => Set<GunSonuRapor>();

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

            // ===== ÜRÜN GÖRSEL =====
            modelBuilder.Entity<UrunGorsel>(entity =>
            {
                entity.ToTable("UrunGorseller");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Data).IsRequired();

                entity.HasOne(e => e.Urun)
                      .WithMany()
                      .HasForeignKey(e => e.UrunId)
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

                // Concurrency: PostgreSQL'de IsRowVersion() çalışmaz, IsConcurrencyToken kullan
                entity.Property(e => e.RowVersion)
                    .IsConcurrencyToken()
                    .HasColumnType("bytea");

                // Indexes
                entity.HasIndex(e => e.Durum).HasDatabaseName("IX_Siparisler_Durum");
                entity.HasIndex(e => e.MasaId).HasDatabaseName("IX_Siparisler_MasaId");
                entity.HasIndex(e => new { e.OturumId, e.OlusturmaTarihi }).HasDatabaseName("IX_Siparisler_OturumId_OlusturmaTarihi");

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

            // ===== KULLANICI (ASP.NET Identity) =====
            // Identity kendi tablolarını (AspNetUsers, AspNetRoles, vb.) otomatik yönetir.
            // Sadece özel alanları yapılandırıyoruz.
            modelBuilder.Entity<Kullanici>(entity =>
            {
                entity.Property(e => e.AdSoyad).IsRequired().HasMaxLength(100);
                // Rol ve AktifMi default değerleri EF ile yönetilir
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

            // ===== HAPPY HOUR =====
            modelBuilder.Entity<HappyHour>(entity =>
            {
                entity.ToTable("HappyHour");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IndirimOrani).HasPrecision(5, 2);

                entity.HasOne(e => e.Urun)
                      .WithMany()
                      .HasForeignKey(e => e.UrunId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<HappyHourUrun>(entity =>
            {
                entity.ToTable("HappyHourUrunler");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.HappyHourId, e.UrunId }).IsUnique();

                entity.HasOne(e => e.HappyHour)
                    .WithMany(h => h.HappyHourUrunler)
                    .HasForeignKey(e => e.HappyHourId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Urun)
                    .WithMany()
                    .HasForeignKey(e => e.UrunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== GUN SONU RAPOR =====
            modelBuilder.Entity<GunSonuRapor>(entity =>
            {
                entity.ToTable("GunSonuRaporlari");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tarih).IsRequired();
                entity.Property(e => e.ToplamCiro).HasPrecision(18, 2);
                entity.Property(e => e.OdemeTipleriJson).HasColumnType("text");
                entity.Property(e => e.KapatanKullaniciId).HasMaxLength(450);
                entity.HasIndex(e => e.Tarih).IsUnique();
            });

            // ===== SEED DATA =====
            SeedData.Seed(modelBuilder);
        }
    }
}
