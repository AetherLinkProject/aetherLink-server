using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using AetherLink.Worker.Core.Provider;
using AetherLink.Indexer.Provider;
using AetherLink.Worker.Core.Provider.SearcherProvider;
using AetherLink.Worker.Core.Dtos;
using System.Linq;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Common;

namespace AetherLink.Worker.Core.Service;

public interface ICompensationForCanceledTasksService
{
    Task StartAsync();
}

public class CompensationForCanceledTasksService : ICompensationForCanceledTasksService, ISingletonDependency
{
    private readonly IWorkerProvider _workerProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IAeFinderProvider _aeFinderProvider;
    private readonly ILogger<CompensationForCanceledTasksService> _logger;

    public CompensationForCanceledTasksService(IAeFinderProvider aeFinderProvider, IWorkerProvider workerProvider,
        IStorageProvider storageProvider, ILogger<CompensationForCanceledTasksService> logger)
    {
        _logger = logger;
        _workerProvider = workerProvider;
        _storageProvider = storageProvider;
        _aeFinderProvider = aeFinderProvider;
    }

    public async Task StartAsync()
    {
        try
        {
            var chainStates = await _aeFinderProvider.GetChainSyncStateAsync();
            await Task.WhenAll(chainStates.Select(async chain =>
            {
                try
                {
                    var chainId = chain.ChainId;
                    var targetHeight = chain.LastIrreversibleBlockHeight;
                    var startHeight = await GetCompensationStartHeightAsync(chainId);
                    if (targetHeight <= startHeight) return;

                    var from = startHeight + 1;
                    var to = targetHeight;

                    _logger.LogInformation($"[Compensation] {chainId} startHeight: {from}, targetHeight: {to}.");

                    var cancels = await _workerProvider.SearchRequestCanceledAsync(chainId, to, from);

                    _logger.LogInformation($"[Compensation] {chainId} found {cancels.Count} canceled tasks.");

                    await Task.WhenAll(cancels.Select(_workerProvider.HandleRequestCancelledLogEventAsync));
                    await SetLatestCompensationHeightAsync(chainId, to);

                    _logger.LogInformation($"[Compensation] {chainId} compensation finished, height updated to {to}.");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[Compensation] CompensationForCanceledTasksService failed for chain.");
                }
            }));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Compensation] CompensationForCanceledTasksService failed.");
        }
    }

    private static string GetCompensationHeightRedisKey(string chainId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.CompensationForCanceledTasksHeight, chainId);

    private async Task<long> GetCompensationStartHeightAsync(string chainId)
    {
        var dto = await _storageProvider.GetAsync<SearchHeightDto>(GetCompensationHeightRedisKey(chainId));
        return dto?.BlockHeight ?? 0;
    }

    private async Task SetLatestCompensationHeightAsync(string chainId, long height)
    {
        await _storageProvider.SetAsync(GetCompensationHeightRedisKey(chainId),
            new SearchHeightDto { BlockHeight = height });
    }
}