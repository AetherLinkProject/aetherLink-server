using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Reporter;
using Io.Gate.GateApi.Api;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface IGateIoProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
}

public class GateIoProvider : IGateIoProvider, ITransientDependency
{
    private readonly IPriceCollectReporter _reporter;

    public GateIoProvider(IPriceCollectReporter reporter)
    {
        _reporter = reporter;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(GateIoProvider), MethodName = nameof(HandleException),
        FinallyMethodName = nameof(FinallyHandler))]
    public virtual async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var currencyPair = await new SpotApi().ListTickersAsync(tokenPair.Replace("-", "_"));

        if (currencyPair == null || currencyPair.Count == 0)
            throw new UserFriendlyException("[GateIo] Get token {tokenPair} price error.");

        var price = PriceConvertHelper.ConvertPrice(double.Parse(currencyPair[0].Last));

        _reporter.RecordPriceCollected(SourceType.GateIo, tokenPair, price);

        return price;
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow,
        };
    }

    public async Task FinallyHandler(string tokenPair)
    {
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.GateIo, tokenPair);
        timer.ObserveDuration();
    }

    #endregion
}