using System.Collections.Generic;

namespace QRMenu.Core.DTOs
{
    public class ManuelSiparisDetayDto
    {
        public int UrunId { get; set; }
        public int? UrunVaryasyonId { get; set; }
        public int Adet { get; set; }
        public List<int>? OpsiyonIds { get; set; }
    }
}
