namespace Infosphere.Api.Requests.V0;

public sealed record TransitionTaskRequest(
    int StateId,
    Guid? SessionId);
