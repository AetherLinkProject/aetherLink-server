using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.PeerManager;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IJobRequestProvider
{
    Task SetJobRequestAsync(RequestDto request);
    Task SetDataMessageAsync(DataMessageDto dataMessage);
    Task SetReportAsync(ReportDto report);
    Task<RequestDto> GetJobRequestAsync(string chainId, string requestId, long epoch);
    Task<DataMessageDto> GetDataMessageAsync(string chainId, string requestId, long epoch);
    Task<List<long>> GetReportAsync(string chainId, string requestId, long epoch);
}

public class JobRequestProvider : AbpRedisCache, IJobRequestProvider, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly ILogger<JobRequestProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;

    public JobRequestProvider(IOptions<RedisCacheOptions> optionsAccessor, IDistributedCacheSerializer serializer,
        ILogger<JobRequestProvider> logger, IPeerManager peerManager) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
        _peerManager = peerManager;
    }

    public async Task SetJobRequestAsync(RequestDto request)
    {
        _logger.LogDebug(
            "[JobRequestProvider] Start to set request. ReqId:{ReqId}, roundId:{RoId}, state:{state}, epoch:{epoch}",
            request.RequestId, request.RoundId, request.State, request.Epoch);
        try
        {
            var configDigest = await GetConfigDigestAsync(request.ChainId);
            await ConnectAsync();
            await RedisDatabase.StringSetAsync(
                GetJobRequestRedisKey(request.ChainId, configDigest, request.RequestId, request.Epoch),
                _serializer.Serialize(request));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set request to redis error.");
        }
    }

    public async Task SetDataMessageAsync(DataMessageDto data)
    {
        _logger.LogDebug(
            "[JobRequestProvider] Start to set data message. ReqId:{ReqId}, roundId:{RoId}, epoch:{epoch}",
            data.RequestId, data.RoundId, data.Epoch);
        try
        {
            var configDigest = await GetConfigDigestAsync(data.ChainId);
            await ConnectAsync();
            await RedisDatabase.StringSetAsync(
                GetDataMessageRedisKey(data.ChainId, configDigest, data.RequestId, data.Epoch),
                _serializer.Serialize(data));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set data message to redis error.");
        }
    }

    public async Task SetReportAsync(ReportDto report)
    {
        _logger.LogDebug(
            "[JobRequestProvider] Start to set report. Report:{Report}, chainId:{ChainId}, reqId:{ReqId}, roundId:{RoId}, epoch:{epoch}",
            report.Observations, report.ChainId, report.RequestId, report.RoundId, report.Epoch);
        try
        {
            var configDigest = await GetConfigDigestAsync(report.ChainId);
            await ConnectAsync();
            await RedisDatabase.StringSetAsync(
                GetReportRedisKey(report.ChainId, configDigest, report.RequestId, report.Epoch),
                _serializer.Serialize(report.Observations));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set report to redis error.");
        }
    }

    private async Task<string> GetConfigDigestAsync(string chainId)
    {
        var configDigest = await _peerManager.GetLatestConfigDigestAsync(chainId);
        return configDigest.ToHex();
    }

    public async Task<RequestDto> GetJobRequestAsync(string chainId, string requestId, long epoch)
    {
        var configDigest = await GetConfigDigestAsync(chainId);
        var redisKey = GetJobRequestRedisKey(chainId, configDigest, requestId, epoch);
        var jobRequest = await GetRedisValueAsync(redisKey);
        if (!jobRequest.HasValue) return null;

        _logger.LogDebug("[JobRequestProvider] {key} request spec: {spec}", redisKey, jobRequest);
        return _serializer.Deserialize<RequestDto>(jobRequest);
    }

    public async Task<DataMessageDto> GetDataMessageAsync(string chainId, string requestId,
        long epoch)
    {
        var configDigest = await GetConfigDigestAsync(chainId);
        var redisKey = GetDataMessageRedisKey(chainId, configDigest, requestId, epoch);
        var dataMessage = await GetRedisValueAsync(redisKey);
        if (!dataMessage.HasValue)
        {
            return null;
        }

        _logger.LogDebug("[JobRequestProvider] {key} dataMessage spec: {spec}", redisKey, dataMessage);
        var arg = _serializer.Deserialize<DataMessageDto>(dataMessage);
        return arg;
    }

    public async Task<List<long>> GetReportAsync(string chainId, string requestId, long epoch)
    {
        var configDigest = await GetConfigDigestAsync(chainId);
        var redisKey = GetReportRedisKey(chainId, configDigest, requestId, epoch);
        var reportValue = await GetRedisValueAsync(redisKey);

        if (!reportValue.HasValue) return null;

        return _serializer.Deserialize<List<long>>(reportValue);
    }

    private async Task<RedisValue> GetRedisValueAsync(string redisKey)
    {
        await ConnectAsync();
        var value = await RedisDatabase.StringGetAsync(redisKey);

        if (!value.HasValue) _logger.LogWarning("No value. Redis key:{Key}", redisKey);

        return value;
    }

    private static string GetJobRequestRedisKey(string chainId, string configDigest, string requestId, long epoch)
    {
        return IdGeneratorHelper.GenerateId(RedisKeyConst.JobRequestRedisKey, chainId, configDigest, requestId, epoch);
    }

    private static string GetDataMessageRedisKey(string chainId, string configDigest, string requestId, long epoch)
    {
        return IdGeneratorHelper.GenerateId(RedisKeyConst.DataMessageRedisKey, chainId, configDigest, requestId, epoch);
    }

    private static string GetReportRedisKey(string chainId, string configDigest, string requestId, long epoch)
    {
        return IdGeneratorHelper.GenerateId(RedisKeyConst.ReportRedisKey, chainId, configDigest, requestId, epoch);
    }
}