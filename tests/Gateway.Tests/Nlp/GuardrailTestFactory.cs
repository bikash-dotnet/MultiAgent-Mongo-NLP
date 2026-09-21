using Gateway.Governance;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

internal static class GuardrailTestFactory
{
    public static GuardrailEvaluator FromAssets(IApprovalFlagStore? flags = null)
    {
        return new GuardrailEvaluator(
            SchemaWhitelist.LoadFromSchemaFile(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt")),
            InMemorySensitiveFieldRegistry.LoadFromDirectory(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema")),
            flags ?? new InMemoryApprovalFlagStore(true));
    }
}
