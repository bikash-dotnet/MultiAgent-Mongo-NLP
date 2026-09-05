namespace Gateway.Greeting;

public static class GreetingClock
{
    public static string ResolvePeriod(TimeOnly localTime)
    {
        if (localTime.Hour is >= 5 and < 12)
        {
            return "morning";
        }

        if (localTime.Hour is >= 12 and < 17)
        {
            return "afternoon";
        }

        return "evening";
    }

    public static string FormatMessage(string displayName, string period)
    {
        return $"Hi {displayName}, Good {period}";
    }
}
