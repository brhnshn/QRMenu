namespace QRMenu.Core.Entities
{
    public class OyunOdul
    {
        public int Id { get; set; }
        public int OyunAyarId { get; set; }
        public string OdulTanim { get; set; } = string.Empty; // "%10 İndirim", "Ücretsiz Tatlı" vb.
        
        /// <summary>
        /// İndirim yüzdesi (örn: 10 -> %10 indirim). 
        /// </summary>
        public decimal IndirimYuzdesi { get; set; }
        
        /// <summary>
        /// Sabit bir indirim tutarı varsa o. Yüzde yoksa bu geçerli sayılabilir.
        /// </summary>
        public decimal IndirimTutari { get; set; }
        
        /// <summary>
        /// Çıkma ihtimali (örn: 50 -> %50 ihtimal)
        /// </summary>
        public decimal IhtimalYuzdesi { get; set; }

        public OyunAyar OyunAyar { get; set; } = null!;
    }
}
