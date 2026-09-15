using Gateway.Nlp.Llm;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class SelfCorrectionTests
{
    private sealed class ScriptedGenerator : ILlmQueryGenerator
    {
        private readonly Queue<string> _outputs;
        public int Calls { get; private set; }

        public ScriptedGenerator(params string[] outputs) => _outputs = new Queue<string>(outputs);

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            Calls++;
            var output = _outputs.Count > 0 ? _outputs.Dequeue() : "not json";
            return Task.FromResult(new LlmQueryResult(output, 5));
        }
    }

    private static SelfCorrectingLlmQueryGenerator Sut(ILlmQueryGenerator generator, int maxAttempts = 3) =>
        new(generator, new PipelineValidator(), Options.Create(new NvidiaNimOptions { MaxAttempts = maxAttempts }));

    [Fact]
    public async Task Returns_first_valid_pipeline()
    {
        var generator = new ScriptedGenerator("not json", """[{"$limit":5}]""");

        var result = await Sut(generator).GenerateAsync("q", "{}", default);

        Assert.Equal(2, result.Attempts);
        Assert.Equal("""[{"$limit":5}]""", result.Pipeline);
        Assert.Null(result.Error);
        Assert.Equal(10, result.TokensConsumed);
    }

    [Fact]
    public async Task Stops_after_three_invalid_attempts()
    {
        var generator = new ScriptedGenerator("bad", "worse", "still bad", "never used");

        var result = await Sut(generator).GenerateAsync("q", "{}", default);

        Assert.Null(result.Pipeline);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, generator.Calls);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Feeds_previous_error_back_to_generator()
    {
        string? seenError = null;
        var generator = new RecordingGenerator(error => seenError = error);

        await Sut(generator, maxAttempts: 2).GenerateAsync("q", "{}", default);

        Assert.False(string.IsNullOrWhiteSpace(seenError));
    }

    private sealed class RecordingGenerator : ILlmQueryGenerator
    {
        private readonly Action<string?> _onCall;

        public RecordingGenerator(Action<string?> onCall) => _onCall = onCall;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            _onCall(previousError);
            return Task.FromResult(new LlmQueryResult("bad", 1));
        }
    }
}
