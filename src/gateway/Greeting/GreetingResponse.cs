namespace Gateway.Greeting;

public sealed record GreetingResponse(
    string DisplayName,
    string Period,
    string Message,
    IReadOnlyList<string> Chips);
