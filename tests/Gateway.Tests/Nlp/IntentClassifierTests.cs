using Gateway.Nlp.Intent;

namespace Gateway.Tests.Nlp;

public class IntentClassifierTests
{
    private readonly IntentClassifier _classifier = new();

    [Theory]
    [InlineData("show listings with pools in Los Angeles", IntentKind.Search, false)]
    [InlineData("find 2 bedroom apartments under 200", IntentKind.Search, false)]
    [InlineData("just run it", IntentKind.Search, false)]
    [InlineData("export the results to csv", IntentKind.Export, false)]
    [InlineData("download listings as xlsx", IntentKind.Export, false)]
    [InlineData("what can you do?", IntentKind.Clarify, false)]
    [InlineData("hi", IntentKind.Clarify, false)]
    [InlineData("average price by market", IntentKind.Search, true)]
    [InlineData("coziest neighborhoods near the beach by season", IntentKind.Search, true)]
    public void Classifies_utterance(string utterance, IntentKind expectedKind, bool expectedComplex)
    {
        var result = _classifier.Classify(utterance);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedComplex, result.IsComplex);
    }
}
