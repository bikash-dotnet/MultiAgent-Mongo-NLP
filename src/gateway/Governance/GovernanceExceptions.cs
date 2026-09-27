namespace Gateway.Governance;

public sealed class GovernanceNotFoundException(string id)
    : Exception($"Access request '{id}' was not found.");

public sealed class GovernanceForbiddenException(string message) : Exception(message);

public sealed class GovernanceConflictException(string message) : Exception(message);

public sealed class GovernanceValidationException(string message) : Exception(message);
