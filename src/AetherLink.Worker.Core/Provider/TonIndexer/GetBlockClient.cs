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

public class GetBlockClient : TonIndexerBase, ISingletonDependency
{
    private readonly TonGetBlockProviderOptions _getBlockConfig;
    private readonly IHttpClientFactory _clientFactory;
    private readonly RequestLimit _requestLimit;

    public GetBlockClient(IOptionsSnapshot<TonGetBlockProviderOptions> snapshotConfig,
        IOptionsSnapshot<TonPublicOptions> tonPublicOptions, IHttpClientFactory clientFactory,
        IStorageProvider storageProvider, ILogger<GetBlockClient> logger) : base(tonPublicOptions, logger)
    {
        _getBlockConfig = snapshotConfig.Value;
        _clientFactory = clientFactory;
        _requestLimit = new RequestLimit(_getBlockConfig.ApiKeyPerSecondRequestLimit,
            _getBlockConfig.ApiKeyPerDayRequestLimit,
            _getBlockConfig.ApiKeyPerMonthRequestLimit, storageProvider);
        ApiWeight = _getBlockConfig.Weight;
        ProviderName = TonStringConstants.GetBlock;
    }

    public override async Task<bool> TryGetRequestAccess()
    {
        return await _requestLimit.TryApplyAccess();
    }

    protected override string AssemblyUrl(string path)
    {
        return
            $"{_getBlockConfig.Url}{(_getBlockConfig.Url.EndsWith("/") ? "" : "/")}{_getBlockConfig.ApiKey}{(path.StartsWith("/") ? "" : "/")}{path}";
    }

    protected override HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        return client;
    }
}

public class RequestLimit
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly IStorageProvider _storageProvider;
    private const string StorageKey = "GetBlockRequestedDetail";

    private readonly int _perSecondRequestLimit;
    private readonly int _perDayRequestLimit;
    private readonly int _perMonthRequestLimit;

    private int _requestCount;

    private RequestedDetail _requestedDetail;

    public RequestLimit(int perSecondRequestLimit, int perDayRequestLimit, int perMonthRequestLimit,
        IStorageProvider storageProvider)
    {
        _perSecondRequestLimit = perSecondRequestLimit;
        _perDayRequestLimit = perDayRequestLimit;
        _perMonthRequestLimit = perMonthRequestLimit;
        _storageProvider = storageProvider;
    }

    public async Task<bool> TryApplyAccess()
    {
        await _semaphore.WaitAsync();
        try
        {
            // init requested detail
            if (_requestedDetail == null)
            {
                _requestedDetail = await _storageProvider.GetAsync<RequestedDetail>(StorageKey);
                if (_requestedDetail == null)
                {
                    _requestedDetail = new RequestedDetail();
                }
            }

            var secondRequest = _requestedDetail.SecondRequest;
            var secondTime = _requestedDetail.SecondTime;

            var dayRequest = _requestedDetail.DayRequest;
            var dateTime = _requestedDetail.DateTime;

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

            // judge day
            if (_perDayRequestLimit > 0)
            {
                var nowDate = GetUtcSecond(DateTime.Today.ToUniversalTime());
                if (nowDate == dateTime)
                {
                    if (dayRequest >= _perDayRequestLimit)
                    {
                        return false;
                    }

                    dayRequest += 1;
                }
                else
                {
                    dayRequest = 1;
                    dateTime = nowDate;
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

            _requestedDetail.DayRequest = dayRequest;
            _requestedDetail.DateTime = dateTime;

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

public class RequestedDetail
{
    public int SecondRequest { get; set; }
    public long SecondTime { get; set; }
    public int DayRequest { get; set; }
    public long DateTime { get; set; }
    public int MonthRequest { get; set; }
    public long Month { get; set; }
}