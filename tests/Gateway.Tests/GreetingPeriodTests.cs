using Gateway.Greeting;

namespace Gateway.Tests;

public class GreetingPeriodTests
{
    [Theory]
    [InlineData(9, 15, "morning")]
    [InlineData(11, 59, "morning")]
    [InlineData(12, 0, "afternoon")]
    [InlineData(16, 59, "afternoon")]
    [InlineData(17, 0, "evening")]
    [InlineData(2, 0, "evening")]
    public void Resolves_period_from_local_clock(int hour, int minute, string expected)
    {
        var period = GreetingClock.ResolvePeriod(new TimeOnly(hour, minute));

        Assert.Equal(expected, period);
    }

    [Fact]
    public void Formats_personalized_message_without_model_calls()
    {
        var message = GreetingClock.FormatMessage("Bikash", "morning");

        Assert.Equal("Hi Bikash, Good morning", message);
    }
}
