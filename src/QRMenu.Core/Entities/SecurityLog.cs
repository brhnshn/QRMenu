namespace QRMenu.Core.Entities
{
    public class SecurityLog
    {
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty; // "RateLimit", "Unauthorized", "Forbidden", "Error"
        public string Message { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? Path { get; set; }
        public string? Method { get; set; }
        public string? UserAgent { get; set; }
        public string? UserId { get; set; } // Nullable if not logged in
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
