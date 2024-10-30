using System;
using System.Linq;
using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Io.Gate.GateApi.Client;
using Serilog;

namespace AetherlinkPriceServer.Worker;

public class GateIoPriceSearchWorker : TokenPriceSearchWorkerBase
{
    private readonly ILogger _logger;
    private readonly TokenPriceSourceOption _option;
    private readonly IGateIoProvider _gateIoProvider;
    protected override SourceType SourceType => SourceType.GateIo;

    public GateIoPriceSearchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TokenPriceSourceOptions> options, IPriceProvider priceProvider, IGateIoProvider gateIoProvider)
        : base(timer, serviceScopeFactory, options, priceProvider)
    {
        _gateIoProvider = gateIoProvider;
        _option = options.Value.GetSourceOption(SourceType);
        _logger = Log.ForContext<GateIoPriceSearchWorker>();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.Information("[GateIo] Search worker Start...");

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
            _logger.Warning(ae, "[GateIo] Connection error.");
            return new();
        }
        catch (Exception e)
        {
            _logger.Error(e, $"[GateIo] Can not get {tokenPair} current price.");
            return new();
        }
    }
}