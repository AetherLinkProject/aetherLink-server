using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Io.Gate.GateApi.Api;
using Volo.Abp;

namespace AetherlinkPriceServer.Worker;

public class GateIoPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    protected override SourceType SourceType => SourceType.GateIo;

    public GateIoPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider) : base(timer, serviceScopeFactory, options, baseLogger, priceProvider)
    {
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[GateIo] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.GateIo,
            await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync)));
    }

    private async Task<KeyValuePair<string, PriceDto>> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            var currencyPair = await new SpotApi().ListTickersAsync(tokenPair.Replace("-", "_"));

            if (currencyPair == null || currencyPair.Count == 0)
                throw new UserFriendlyException("[GateIo] Get token {tokenPair} price error.");

            return new KeyValuePair<string, PriceDto>(tokenPair, new PriceDto
            {
                TokenPair = tokenPair,
                Price = PriceConvertHelper.ConvertPrice(double.Parse(currencyPair[0].Last)),
                UpdateTime = DateTime.Now
            });
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[GateIo] Can not get {tokenPair} current price.");
            return new();
        }
    }
}