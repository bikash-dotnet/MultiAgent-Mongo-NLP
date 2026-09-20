using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

internal static class GuardrailTestFactory
{
    public static GuardrailEvaluator FromAssets()
    {
        return new GuardrailEvaluator(
            SchemaWhitelist.LoadFromSchemaFile(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt")),
            InMemorySensitiveFieldRegistry.LoadFromDirectory(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema")));
    }
}
