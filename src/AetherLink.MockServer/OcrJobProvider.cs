using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.MockServer;

public class OcrJobProvider : ISingletonDependency
{
    private IOptionsMonitor<OcrJobOptions> _options;

    public OcrJobProvider(IOptionsMonitor<OcrJobOptions> options)
    {
        _options = options;
    }

    public long GetLastHeight()
    {
        return _options.CurrentValue.LastHeight;
    }
}