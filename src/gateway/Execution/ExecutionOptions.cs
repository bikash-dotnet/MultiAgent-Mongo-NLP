namespace Gateway.Execution;

public sealed class ExecutionOptions
{
    public const string SectionName = "Execution";

    public ExecutionDataSource DataSource { get; set; } = ExecutionDataSource.Mongo;

    public int TimeoutMs { get; set; } = 5000;

    public string Collection { get; set; } = "listingsAndReviews";

    public string Database { get; set; } = "sample_airbnb";
}
