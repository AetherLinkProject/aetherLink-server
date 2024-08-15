using System;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Io.Gate.GateApi.Client;

namespace AetherlinkPriceServer.Worker;

public class GateIoPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly TokenPriceSourceOption _option;
    private readonly IGateIoProvider _gateIoProvider;
    protected override SourceType SourceType => SourceType.GateIo;

    public GateIoPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, ILogger<TokenPriceSearchWorkerBase> baseLogger,
        IPriceProvider priceProvider, IGateIoProvider gateIoProvider) : base(timer, serviceScopeFactory, options,
        baseLogger, priceProvider)
    {
        _gateIoProvider = gateIoProvider;
        _option = options.Value.GetSourceOption(SourceType);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        BaseLogger.LogInformation("[GateIo] Search worker Start...");

        await PriceProvider.UpdatePricesAsync(SourceType.GateIo,
            (await Task.WhenAll(_option.Tokens.Select(SearchTokenPriceAsync))).ToList());
    }

    private async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        try
        {
            return new()
            {
                TokenPair = tokenPair,
                Price = await _gateIoProvider.GetTokenPriceAsync(tokenPair),
                UpdateTime = DateTime.Now
            };
        }
        catch (ApiException ae)
        {
            BaseLogger.LogWarning(ae, "[GateIo] Connection error.");
            return new();
        }
        catch (Exception e)
        {
            BaseLogger.LogError(e, $"[GateIo] Can not get {tokenPair} current price.");
            return new();
        }
    }
}