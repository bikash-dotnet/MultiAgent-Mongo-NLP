namespace Gateway.Nlp.Router;

public interface INlpRouter
{
    NlpRouteResult Route(string utterance);
}
