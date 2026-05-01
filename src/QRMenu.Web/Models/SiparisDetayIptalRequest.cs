using System.Collections.Generic;
using QRMenu.Core.DTOs;

namespace QRMenu.Web.Models
{
    public class SiparisDetayIptalRequest
    {
        public int? MasaId { get; set; }
        public List<SiparisDetayIptalDto> Detaylar { get; set; } = new();
    }
}
