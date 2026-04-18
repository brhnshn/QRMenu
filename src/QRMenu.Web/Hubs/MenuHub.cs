using Microsoft.AspNetCore.SignalR;
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
    /// Sipariş akışlarının gerçek zamanlı dağıtımı için merkezi SignalR hub.
    /// </summary>
    public class OrderHub : Hub
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();

        public override async Task OnConnectedAsync()
        {
            foreach (var group in ResolveRoleGroups())
            {
                await TrackAndJoinGroupAsync(group);
            }

            var http = Context.GetHttpContext();
            if (http != null && int.TryParse(http.Request.Query["tableId"], out var tableId) && tableId > 0)
            {
                await TrackAndJoinGroupAsync(SignalRGroups.Table(tableId));
            }

            await base.OnConnectedAsync();
        }

        public async Task JoinTableGroup(int tableId)
        {
            if (tableId <= 0) return;
            await TrackAndJoinGroupAsync(SignalRGroups.Table(tableId));
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
