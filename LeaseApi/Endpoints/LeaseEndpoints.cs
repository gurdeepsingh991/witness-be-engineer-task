using LeaseApi.Services;

namespace LeaseApi.Endpoints;

public static class LeaseEndpoints
{
    public static void MapLeaseEndpoints(this WebApplication app)
    {
        app.MapGet("/{titleNumber}", async (
                string titleNumber,
                LeaseOrchestrator orchestrator,
                CancellationToken ct) =>
            await orchestrator.HandleAsync(titleNumber, ct))
        .WithName("GetLeaseByTitleNumber")
        .WithOpenApi();
    }
}