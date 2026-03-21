namespace Infosphere.Api.Responses.V0;

public sealed record SystemOverviewResponse(
    string Service,
    string ApiVersion,
    string OpenApiDocumentPath,
    string? DashboardPath,
    IReadOnlyList<string> HealthPaths);
