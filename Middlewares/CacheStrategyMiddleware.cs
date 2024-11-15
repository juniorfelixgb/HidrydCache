using HidrydCache.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace HidrydCache.Middlewares;

internal sealed class CacheStrategyMiddleware : IMiddleware
{
    private readonly IHybridCache _hybridCache;
    private readonly ILogger<CacheStrategyMiddleware> _logger;

    public CacheStrategyMiddleware(IHybridCache hybridCache, ILogger<CacheStrategyMiddleware> logger)
    {
        _hybridCache = hybridCache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var cacheKey = GenerateCacheKey(context.Request);

            var response = await _hybridCache.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var originalBodyStream = context.Response.Body;
                    using var memoryStream = new MemoryStream();
                    context.Response.Body = memoryStream;

                    await next(context);

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
                    context.Response.Body = originalBodyStream;

                    return responseBody;
                },
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response);

            return;
        }

        await next(context);
    }

    private static string GenerateCacheKey(HttpRequest request)
    {
        return $"{request.Path}_{request.QueryString}";
    }
}
