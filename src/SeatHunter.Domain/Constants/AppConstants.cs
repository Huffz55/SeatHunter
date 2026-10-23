namespace SeatHunter.Domain.Constants;

public static class AppConstants
{
    public static class Exchanges
    {
        public const string DeadLetterExchange = "hunt_requests_dlx";
    }

    public static class Queues
    {
        public const string HuntRequestsQueue = "hunt_requests_queue";
        public const string HuntRequestsDeadLetterQueue = "hunt_requests_dlq";
    }

    public static class SignalREvents
    {
        public const string SeatFound = "SeatFound";
    }

    public static class RateLimiting
    {
        public const string FixedPolicy = "FixedPolicy";
    }

    public static class Providers
    {
        public const string Universal = "Universal";
        public const string BiletixLive = "BiletixLive";
        public const string ObiletLive = "ObiletLive";
    }

    public static class Metrics
    {
        public const string MeterName = "SeatHunter.Engine";
    }

    public static class Plans
    {
        public static class Free
        {
            public const int MaxActiveHunts = 1;
            public const int MinScanIntervalMinutes = 15;
            public const bool AllowAdvancedCriteria = false;
            public const bool AllowAutoReservation = false;
        }

        public static class Pro
        {
            public const int MaxActiveHunts = 5;
            public const int MinScanIntervalMinutes = 1;
            public const bool AllowAdvancedCriteria = true;
            public const bool AllowAutoReservation = false;
        }

        public static class Vip
        {
            public const int MaxActiveHunts = 25;
            public const int MinScanIntervalMinutes = 0;
            public const bool AllowAdvancedCriteria = true;
            public const bool AllowAutoReservation = true;
        }
    }
}
