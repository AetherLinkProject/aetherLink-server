using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Aetherlink.PriceServer.Common;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Common;
using AetherlinkPriceServer.Options;
using AetherlinkPriceServer.Reporter;
using AetherlinkPriceServer.Worker.Dtos;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherlinkPriceServer.Provider;

public interface ICoinBaseProvider
{
    public Task<long> GetTokenPriceAsync(string tokenPair);
    public Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time);
}

public class CoinBaseProvider : ICoinBaseProvider, ITransientDependency
{
    private readonly IHttpService _http;
    private readonly TokenPriceSourceOption _option;
    private readonly IPriceCollectReporter _reporter;

    public CoinBaseProvider(IHttpService http, IOptionsSnapshot<TokenPriceSourceOptions> options,
        IPriceCollectReporter reporter)
    {
        _http = http;
        _reporter = reporter;
        _option = options.Value.GetSourceOption(SourceType.CoinBase);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(CoinBaseProvider), MethodName = nameof(HandleException),
        FinallyMethodName = nameof(FinallyHandler))]
    public virtual async Task<long> GetTokenPriceAsync(string tokenPair)
    {
        var price = PriceConvertHelper.ConvertPrice(double.Parse(
            (await _http.GetAsync<CoinBaseResponseDto>(_option.BaseUrl + $"{tokenPair}/buy",
                ContextHelper.GeneratorCtx())).Data["amount"]));

        _reporter.RecordPriceCollected(SourceType.CoinBase, tokenPair, price);

        return price;
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(CoinBaseProvider), MethodName = nameof(HandleException),
        FinallyMethodName = nameof(FinallyHandler))]
    public virtual async Task<long> GetHistoricPriceAsync(string tokenPair, DateTime time)
    {
        return PriceConvertHelper.ConvertPrice(double.Parse(
            (await _http.GetAsync<CoinBaseResponseDto>(_option.BaseUrl + $"{tokenPair}/spot?date={time:yyyy-MM-dd}",
                ContextHelper.GeneratorCtx())).Data["amount"]));
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
        var timer = _reporter.GetPriceCollectLatencyTimer(SourceType.CoinBase, tokenPair);
        timer.ObserveDuration();
    }

    #endregion
}