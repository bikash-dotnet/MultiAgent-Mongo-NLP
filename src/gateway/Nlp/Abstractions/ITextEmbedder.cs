namespace Gateway.Nlp.Abstractions;

public interface ITextEmbedder
{
    float[] Embed(string text);
}
