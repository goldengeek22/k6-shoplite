namespace ShopLite.Api.Chaos;

public class ChaosMiddleware(RequestDelegate next, ChaosState state)
{
    private static readonly PathString[] ExcludedPaths = ["/admin", "/health", "/metrics"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (ExcludedPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await next(context);
            return;
        }

        var chaos = state.Current;

        if (chaos.LatencyMs > 0)
        {
            await Task.Delay(chaos.LatencyMs, context.RequestAborted);
        }

        if (chaos.ErrorRate > 0 && Random.Shared.NextDouble() < chaos.ErrorRate)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Chaos: injected failure" });
            return;
        }

        await next(context);
    }    
}
