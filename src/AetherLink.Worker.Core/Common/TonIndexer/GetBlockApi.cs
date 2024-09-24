using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public sealed class GetBlockApi:TonIndexerBase,ISingletonDependency
{
    private readonly GetBlockConfig _apiConfig;
    private readonly IHttpClientFactory _clientFactory;
    private readonly RequestLimit _requestLimit;
    
    public int ApiWeight { get; }

    public GetBlockApi(IOptionsSnapshot<IConfiguration> snapshotConfig, TonHelper tonHelper,
        IHttpClientFactory clientFactory, IStorageProvider storageProvider):base(tonHelper)
    {
        _apiConfig = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton:Indexer:GetBlock").Get<GetBlockConfig>();
        _clientFactory = clientFactory;
        _requestLimit = new RequestLimit(_apiConfig.ApiKeyPerSecondRequestLimit, _apiConfig.ApiKeyPerDayRequestLimit,
            _apiConfig.ApiKeyPerMonthRequestLimit, storageProvider);
    }

    public override async Task<bool> TryGetRequestAccess()
    {
        return await _requestLimit.TryApplyAccess();
    }
    
    protected override string AssemblyUrl(string path)
    {
        return $"{_apiConfig.Url}{(_apiConfig.Url.EndsWith("/") ? "" : "/")}{_apiConfig.ApiKey}{(path.StartsWith("/") ? "" : "/")}{path}";
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
    private const string _storageKey = "GetBlockRequestedDetail"; 
    
    private readonly int _perSecondRequestLimit;
    private readonly int _perDayRequestLimit;
    private readonly int _perMonthRequestLimit;

    private int _requestCount;
    
    private RequestedDetail _requestedDetail; 
    
    public RequestLimit(int perSecondRequestLimit, int perDayRequestLimit, int perMonthRequestLimit, IStorageProvider storageProvider)
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
                _requestedDetail = await _storageProvider.GetAsync<RequestedDetail>(_storageKey);
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
                await _storageProvider.SetAsync(_storageKey, _requestedDetail);
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
        return (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}

public class GetBlockConfig
{
    public string Url { get; set; }
    public int Weight { get; set; }
    public string ApiKey { get; set; }
    public int ApiKeyPerSecondRequestLimit { get; set; }
    public int ApiKeyPerDayRequestLimit { get; set; }
    public int ApiKeyPerMonthRequestLimit { get; set; }
}

public class RequestedDetail
{
    public  int SecondRequest { get; set; }
    public  Int64 SecondTime { get; set; }

    public int DayRequest { get; set; }
    public Int64 DateTime { get; set; }

    public int MonthRequest { get; set; }
    
    public Int64 Month { get; set; }
}