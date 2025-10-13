using System;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace FacebookS;

public class RateLimitedHandler(HttpMessageHandler innerHandler, RateLimiter limiter) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Wait for permission before sending
        using var lease = await limiter.AcquireAsync(1, cancellationToken);

        if (!lease.IsAcquired)
            throw new InvalidOperationException("Rate limit exceeded");

        return await base.SendAsync(request, cancellationToken);
    }
}
