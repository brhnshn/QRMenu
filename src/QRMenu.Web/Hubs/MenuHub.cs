using Microsoft.AspNetCore.SignalR;

namespace QRMenu.Web.Hubs
{
    /// <summary>
    /// Menü sayfasına gerçek zamanlı güncelleme gönderen SignalR Hub.
    /// Admin panelinden yapılan ürün/kategori değişiklikleri anında müşteri menüsüne yansır.
    /// </summary>
    public class MenuHub : Hub
    {
    }
}
