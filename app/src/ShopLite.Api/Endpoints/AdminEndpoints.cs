using ShopLite.Api.Chaos;

namespace ShopLite.Api.Endpoints;

public static class AdminEndpoints
{

    private static IResult UpdateChaos(ChaosConfig config, ChaosState state, ILogger<ChaosState> logger)
    {
        var errors = new Dictionary<string, string[]>();
        if (config.LatencyMs is < 0 or > 60_000)
        {
            errors["latencyMs"] = ["latencyMs must be between 0 and 60000"];
        }
        if (config.ErrorRate is < 0 or > 1)
        {
            errors["errorRate"] = ["errorRate must be between 0.0 and 1.0"];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        state.Update(config);
        logger.LogWarning(
            "Chaos settings updated: LatencyMs={LatencyMs}, ErrorRate={ErrorRate}, SlowQuery={SlowQuery}",
            config.LatencyMs, config.ErrorRate, config.SlowQuery);
            
        return TypedResults.Ok(config);
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin");

        group.MapGet("/chaos", (ChaosState state) => TypedResults.Ok(state.Current));
        group.MapPost("/chaos", UpdateChaos);

        return app;
    }
}
