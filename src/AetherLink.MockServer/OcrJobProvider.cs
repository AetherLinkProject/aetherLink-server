using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.MockServer;

public class OcrJobProvider : ISingletonDependency
{
    private IOptionsMonitor<OcrJobOptions> _options;
    private int _latestHeight = 1000;

    public OcrJobProvider(IOptionsMonitor<OcrJobOptions> options)
    {
        _options = options;
    }

    public long GetLastHeight()
    {
        _latestHeight += 1000;
        return _latestHeight;
    }
}