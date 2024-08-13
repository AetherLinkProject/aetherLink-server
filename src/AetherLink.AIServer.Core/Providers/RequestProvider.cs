using System;
using System.Threading.Tasks;
using AetherLink.AIServer.Core.Dtos;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AetherLink.AIServer.Core.Providers;

public interface IRequestProvider
{
    Task SetAsync(string id, RequestStorageDto data);
    Task<RequestStorageDto> GetAsync(string id);
}

public class RequestProvider : IRequestProvider, ITransientDependency
{
    private readonly ILogger<RequestProvider> _logger;
    private readonly IStorageProvider _storageProvider;

    public RequestProvider(ILogger<RequestProvider> logger, IStorageProvider storageProvider)
    {
        _logger = logger;
        _storageProvider = storageProvider;
    }

    public async Task SetAsync(string id, RequestStorageDto data)
    {
        try
        {
            await _storageProvider.SetAsync(id, data);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[RequestProvider] Set {id} to redis error.");
        }
    }

    public async Task<RequestStorageDto> GetAsync(string id)
    {
        try
        {
            return await _storageProvider.GetAsync<RequestStorageDto>(id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[RequestProvider] Get {id} error.");
            return null;
        }
    }
}