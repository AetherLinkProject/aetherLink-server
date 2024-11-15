using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider.TonIndexer;

public class ChainStackClient : TonIndexerBase, ISingletonDependency
{
    private readonly ChainStackApiConfig _chainStackConfig;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ChainStackRequestLimit _requestLimit;

    public ChainStackClient(IOptionsSnapshot<ChainStackApiConfig> snapshotConfig,
        IOptionsSnapshot<TonPublicOptions> tonPublicOptions, IHttpClientFactory clientFactory,
        IStorageProvider storageProvider, ILogger<ChainStackClient> logger) : base(tonPublicOptions, logger)
    {
        _chainStackConfig = snapshotConfig.Value;
        _clientFactory = clientFactory;
        _requestLimit = new ChainStackRequestLimit(_chainStackConfig.ApiKeyPerSecondRequestLimit,
            _chainStackConfig.ApiKeyPerMonthRequestLimit, storageProvider);
        ApiWeight = _chainStackConfig.Weight;
        ProviderName = TonStringConstants.ChainStack;
    }

    public override async Task<bool> TryGetRequestAccess()
    {
        return await _requestLimit.TryApplyAccess();
    }

    protected override string AssemblyUrl(string path)
    {
        return
            $"{_chainStackConfig.Url}{(_chainStackConfig.Url.EndsWith("/") ? "" : "/")}{_chainStackConfig.ApiKey}/api/v3{(path.StartsWith("/") ? "" : "/")}{path}";
    }

    protected override HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        return client;
    }
}

public class ChainStackRequestLimit
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly IStorageProvider _storageProvider;
    private const string StorageKey = "ChainStackRequestedDetail";

    private readonly int _perSecondRequestLimit;
    private readonly int _perMonthRequestLimit;

    private int _requestCount;

    private ChainStackRequestedDetail _requestedDetail;

    public ChainStackRequestLimit(int perSecondRequestLimit, int perMonthRequestLimit,
        IStorageProvider storageProvider)
    {
        _perSecondRequestLimit = perSecondRequestLimit;
        _perMonthRequestLimit = perMonthRequestLimit;
        _storageProvider = storageProvider;
    }

    public async Task<bool> TryApplyAccess()
    {
        await _semaphore.WaitAsync();
        try
        {
            // init requested detail
            _requestedDetail ??= await _storageProvider.GetAsync<ChainStackRequestedDetail>(StorageKey) ??
                                 new ChainStackRequestedDetail();

            var secondRequest = _requestedDetail.SecondRequest;
            var secondTime = _requestedDetail.SecondTime;

            var monthRequest = _requestedDetail.MonthRequest;
            var monthTime = _requestedDetail.Month;

            // judge second
            if (_perSecondRequestLimit > 0)
            {
                var dtNow = GetUtcSecond(DateTime.UtcNow);
                if (dtNow == secondTime)
                {
                    if (secondRequest >= _perSecondRequestLimit)
                    {
                        return false;
                    }

                    secondRequest += 1;
                }
                else
                {
                    secondRequest = 1;
                    secondTime = dtNow;
                }
            }

            // judge month
            if (_perMonthRequestLimit > 0)
            {
                var dtNow = DateTime.Now;
                var nowMonth = GetUtcSecond(new DateTime(dtNow.Year, dtNow.Month, 1).ToUniversalTime());
                if (nowMonth == monthTime)
                {
                    if (monthRequest >= _perMonthRequestLimit)
                    {
                        return false;
                    }

                    monthRequest += 1;
                }
                else
                {
                    monthRequest = 1;
                    monthTime = nowMonth;
                }
            }

            _requestedDetail.SecondRequest = secondRequest;
            _requestedDetail.SecondTime = secondTime;

            _requestedDetail.MonthRequest = monthRequest;
            _requestedDetail.Month = monthTime;

            _requestCount += 1;
            if (_requestCount >= 100)
            {
                await _storageProvider.SetAsync(StorageKey, _requestedDetail);
                _requestCount = 0;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return true;
    }

    private Int64 GetUtcSecond(DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeSeconds();
    }
}

public class ChainStackRequestedDetail
{
    public int SecondRequest { get; set; }
    public long SecondTime { get; set; }
    public int MonthRequest { get; set; }
    public long Month { get; set; }
}