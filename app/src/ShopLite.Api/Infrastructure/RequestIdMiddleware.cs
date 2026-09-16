namespace ShopLite.Api.Infrastructure;

public class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    public const string HeaderName = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = context.TraceIdentifier;
        }

        context.Response.Headers[HeaderName] = requestId;

        using (logger.BeginScope(new Dictionary<string, object>{["RequestId"]=requestId}))
        {
            await next(context);
        }
    }
}