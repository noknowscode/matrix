namespace matrix.Services.Endpoints;

internal class DebugHttpHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public DebugHttpHandler(ILogger<DebugHttpHandler> logger, HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new HttpClientHandler())
    {
        _logger = logger;
    }

    protected async override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
#if DEBUG
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebugMessage("Unsuccessful API Call");
            if (request.RequestUri is not null)
            {
                _logger.LogDebugMessage($"{request.RequestUri} ({request.Method})");
            }

            foreach (var header in request.Headers)
            {
                _logger.LogDebugMessage($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            var content = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            if (!string.IsNullOrEmpty(content))
            {
                _logger.LogDebugMessage(content);
            }
        }
#endif
        return response;
    }
}
