using System.Security.Cryptography;

namespace QRMenu.Core.Helpers
{
    public static class QrTokenGenerator
    {
        public static string Generate()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public static string GenerateToken() => Generate();
    }
}
