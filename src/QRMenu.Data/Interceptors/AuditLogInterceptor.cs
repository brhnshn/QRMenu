using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using System.Text.Json;

namespace QRMenu.Data.Interceptors
{
    public class AuditLogInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentUserProvider _currentUserProvider;

        public AuditLogInterceptor(ICurrentUserProvider currentUserProvider)
        {
            _currentUserProvider = currentUserProvider;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            BeforeSavingChanges(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            BeforeSavingChanges(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void BeforeSavingChanges(DbContext? context)
        {
            if (context == null) return;

            var userId = _currentUserProvider.GetUserId();
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                // Sadece kritik entityleri takip et (AuditLog'un kendisini kaydetme - sonsuz döngü olur)
                if (entry.Entity is AuditLog) continue;

                var entityType = entry.Entity.GetType();
                var entityName = entityType.Name;

                // Sadece belirli tabloları logla
                if (entityName != "Siparis" && entityName != "SiparisDetay" && entityName != "Urun" && entityName != "Masa" && entityName != "Kullanici")
                    continue;

                var auditLog = new AuditLog
                {
                    TabloAdi = entityName,
                    Islem = entry.State.ToString().ToUpperInvariant(),
                    IslemTarihi = DateTime.UtcNow,
                    KullaniciId = userId
                };

                // KayitId al (Added durumunda 0 olabilir, ama Modified/Deleted için önemlidir)
                try
                {
                    var idProp = entry.Property("Id");
                    if (idProp != null)
                    {
                        auditLog.KayitId = (int)(idProp.CurrentValue ?? 0);
                    }
                }
                catch { }

                if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    var oldValues = new Dictionary<string, object?>();
                    foreach (var property in entry.OriginalValues.Properties)
                    {
                        oldValues[property.Name] = entry.OriginalValues[property];
                    }
                    auditLog.EskiDeger = JsonSerializer.Serialize(oldValues);
                }

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var newValues = new Dictionary<string, object?>();
                    foreach (var property in entry.CurrentValues.Properties)
                    {
                        newValues[property.Name] = entry.CurrentValues[property];
                    }
                    auditLog.YeniDeger = JsonSerializer.Serialize(newValues);
                }

                // AuditLog'u kaydetmek için context'e ekle
                context.Set<AuditLog>().Add(auditLog);
            }
        }
    }
}
