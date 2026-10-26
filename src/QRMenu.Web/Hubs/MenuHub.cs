using Microsoft.AspNetCore.SignalR;
using QRMenu.Core.Interfaces;
using System.Collections.Concurrent;

namespace QRMenu.Web.Hubs
{
    public static class SignalRGroups
    {
        public const string Kitchen = "kitchen";
        public const string Waiter = "waiter";
        public const string Cashier = "cashier";

        public static string Table(int tableId) => $"table-{tableId}";
    }

    /// <summary>
    /// P4.2: Sipariş akışlarının gerçek zamanlı dağıtımı için güvenli SignalR hub.
    /// Masa gruplarına rastgele katılım engellenmiştir.
    /// </summary>
    public class OrderHub : Hub
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();
        private readonly ITokenService _tokenService;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(ITokenService tokenService, ILogger<OrderHub> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            // 1. Personel rollerine göre gruplara katıl
            foreach (var group in ResolveRoleGroups())
            {
                await TrackAndJoinGroupAsync(group);
            }

            // 2. Müşteri ise masayı istemci parametresinden değil, doğrulanmış cookie'den türet
            var http = Context.GetHttpContext();
            var rawToken = http?.Request.Cookies["qrmenu_token"];
            if (!string.IsNullOrEmpty(rawToken))
            {
                var tokenHash = _tokenService.HashToken(rawToken);
                var oturum = await _tokenService.ValidateTokenAsync(tokenHash);
                if (oturum != null)
                {
                    await TrackAndJoinGroupAsync(SignalRGroups.Table(oturum.MasaId));
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Masa grubuna katılım isteği
        /// Personel için serbesttir; müşteriler için yalnızca kendi masalarına izin verilir.
        /// </summary>
        public async Task JoinTableGroup(int tableId)
        {
            if (tableId <= 0) return;

            // Personel ise izin ver
            if (IsStaffUser())
            {
                await TrackAndJoinGroupAsync(SignalRGroups.Table(tableId));
                return;
            }

            // Müşteri ise doğrulanmış cookie'deki masa kontrolü yap
            var http = Context.GetHttpContext();
            var rawToken = http?.Request.Cookies["qrmenu_token"];
            if (!string.IsNullOrEmpty(rawToken))
            {
                var tokenHash = _tokenService.HashToken(rawToken);
                var oturum = await _tokenService.ValidateTokenAsync(tokenHash);
                if (oturum != null && oturum.MasaId == tableId)
                {
                    await TrackAndJoinGroupAsync(SignalRGroups.Table(tableId));
                    return;
                }
            }

            _logger.LogWarning("Yetkisiz masa grubu dinleme girişimi engellendi. TableId={TableId}, ConnectionId={ConnectionId}",
                tableId, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connectionGroups.TryRemove(Context.ConnectionId, out var groups))
            {
                foreach (var group in groups)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private bool IsStaffUser()
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
                return false;

            return Context.User.IsInRole("Admin") ||
                   Context.User.IsInRole("Garson") ||
                   Context.User.IsInRole("Kasa") ||
                   Context.User.IsInRole("Mutfak") ||
                   Context.User.IsInRole("Barista");
        }

        private IEnumerable<string> ResolveRoleGroups()
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
                yield break;

            if (Context.User.IsInRole("Admin") || Context.User.IsInRole("Mutfak") || Context.User.IsInRole("Barista"))
                yield return SignalRGroups.Kitchen;

            if (Context.User.IsInRole("Admin") || Context.User.IsInRole("Garson"))
                yield return SignalRGroups.Waiter;

            if (Context.User.IsInRole("Admin") || Context.User.IsInRole("Kasa"))
                yield return SignalRGroups.Cashier;
        }

        private async Task TrackAndJoinGroupAsync(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var set = _connectionGroups.GetOrAdd(Context.ConnectionId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            lock (set)
            {
                set.Add(groupName);
            }
        }
    }
}
