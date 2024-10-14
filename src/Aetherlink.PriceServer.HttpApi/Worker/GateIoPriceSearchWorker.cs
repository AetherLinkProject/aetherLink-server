using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(GateIoPriceSearchWorker),
        MethodName = nameof(HandleException))]
    public virtual async Task<PriceDto> SearchTokenPriceAsync(string tokenPair)
    {
        return new()
        {
            TokenPair = tokenPair,
            Price = await _gateIoProvider.GetTokenPriceAsync(tokenPair),
            UpdateTime = DateTime.Now
        };
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex, string tokenPair)
    {
        switch (ex)
        {
            case ApiException ae:
                BaseLogger.LogWarning(ae, "[GateIo] Connection error.");
                break;
            default:
                BaseLogger.LogError(ex, $"[GateIo] Can not get {tokenPair} current price.");
                break;
        }

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PriceDto()
        };
    }

    #endregion
}