using System;

namespace GeneralAffairsManagementProject.Utils
{
    public static class DateTimeUtils
    {
        public static DateTime GetJstNow()
        {
            var jst = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
        }

        public static DateTime ToJst(DateTime utcDateTime)
        {
            var jst = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, jst);
        }
    }
}