namespace QRMenu.Core.Interfaces
{
    public interface ICurrentUserProvider
    {
        int? GetUserId();
        string? GetUserName();
    }
}
