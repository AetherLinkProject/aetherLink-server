using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IHamsterProvider
{
    public Task<List<PriceDto>> GetTokenPriceAsync();
}

public class HamsterProvider : IHamsterProvider, ITransientDependency
{
    private readonly IHttpService _http;
    private ILogger<HamsterProvider> _logger;
    private readonly TokenPriceSourceOption _option;
    private readonly IPriceCollectReporter _reporter;

    public HamsterProvider(IHttpService http, IOptionsSnapshot<TokenPriceSourceOptions> options,
        IPriceCollectReporter reporter, ILogger<HamsterProvider> logger)
    {
        _http = http;
        _logger = logger;
        _reporter = reporter;
        _option = options.Value.GetSourceOption(SourceType.Hamster);
    }

    public async Task<List<PriceDto>> GetTokenPriceAsync()
    {
        var result = new List<PriceDto>();
        var data = await _http.GetAsync<HamsterPriceResponseDto>(_option.BaseUrl, ContextHelper.GeneratorCtx());
        if (data.Code != "20000")
        {
            _logger.LogError("Get HamsterPrice Failed.");
            return result;
        }

        var price = data.Data;

        var acornsElf = new PriceDto
        {
            TokenPair = "acorns-elf",
            Price = PriceConvertHelper.ConvertPrice(price.AcornsInElf),
            UpdateTime = DateTime.Now
        };
        result.Add(acornsElf);
        _reporter.RecordPriceCollected(SourceType.Hamster, acornsElf.TokenPair, acornsElf.Price);

        var acornsUsd = new PriceDto
        {
            TokenPair = "acorns-usd",
            Price = PriceConvertHelper.ConvertPrice(price.AcornsInUsd),
            UpdateTime = DateTime.Now
        };
        result.Add(acornsUsd);
        _reporter.RecordPriceCollected(SourceType.Hamster, acornsUsd.TokenPair, acornsUsd.Price);

        return result;
    }
}