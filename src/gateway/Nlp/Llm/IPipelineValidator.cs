namespace Gateway.Nlp.Llm;

public interface IPipelineValidator
{
    PipelineValidationResult Validate(string pipeline);
}
