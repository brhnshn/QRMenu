using System;

namespace QRMenu.Core.Entities
{
    public class KazanilanIndirim
    {
        public int Id { get; set; }
        public int SiparisId { get; set; }
        public string OdulTanim { get; set; } = string.Empty;
        
        public decimal UgulananIndirimTutari { get; set; }
        
        public DateTime KazanmaTarihi { get; set; } = DateTime.UtcNow;

        public Siparis Siparis { get; set; } = null!;
    }
}
