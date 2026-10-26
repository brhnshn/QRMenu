using System;

namespace QRMenu.Core.Helpers
{
    public static class DateTimeHelper
    {
        public static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        public static DateTime GetTurkeyNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TurkeyTimeZone);
        }

        public static DateTime ToTurkeyTime(DateTime utcDateTime)
        {
            var utc = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TurkeyTimeZone);
        }

        public static DateTime GetTurkeyBusinessDayUtc(DateTime utcDateTime)
        {
            var turkeyTime = ToTurkeyTime(utcDateTime);
            return DateTime.SpecifyKind(turkeyTime.Date, DateTimeKind.Utc);
        }

        public static DateTime GetTurkeyTime(DateTime utcDateTime) => ToTurkeyTime(utcDateTime);

        public static (DateTime StartUtc, DateTime EndUtc) GetTurkeyDayUtcRange(DateTime? localOrTurkeyDate = null)
        {
            var date = localOrTurkeyDate?.Date ?? GetTurkeyNow().Date;
            var startLocal = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, TurkeyTimeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), TurkeyTimeZone);
            return (startUtc, endUtc);
        }
    }
}
