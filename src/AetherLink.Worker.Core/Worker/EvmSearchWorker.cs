using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider.SearcherProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class EvmSearchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly WorkerOptions _options;
    private readonly EvmContractsOptions _evmOptions;
    private readonly ILogger<EvmSearchWorker> _logger;
    private readonly IEvmSearchWorkerProvider _provider;
    private readonly ConcurrentDictionary<string, long> _heightMap = new();
    private readonly ConcurrentDictionary<string, Web3> _evmClientMap = new();

    public EvmSearchWorker(
        AbpAsyncTimer timer,
        ILogger<EvmSearchWorker> logger,
        IEvmSearchWorkerProvider provider,
        IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<WorkerOptions> workerOptions,
        IOptionsSnapshot<EvmContractsOptions> evmOptions
    ) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _provider = provider;
        _evmOptions = evmOptions.Value;
        Timer.Period = workerOptions.Value.EvmSearchTimer;
        Initialize().GetAwaiter().GetResult();
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        => await Task.WhenAll(_evmOptions.ContractConfig.Values.Select(SearchRequestsAsync));

    private async Task SearchRequestsAsync(EvmOptions options)
    {
        var networkName = options.NetworkName;
        var consumedBlockHeight = _heightMap[options.NetworkName];
        try
        {
            var web3 = _evmClientMap[networkName];
            var latestBlock = await _provider.GetLatestBlockHeightAsync(web3);
            if (consumedBlockHeight == 0)
            {
                await SaveBlockHeightAsync(networkName, latestBlock);
                return;
            }

            if (consumedBlockHeight + options.SubscribeBlocksDelay >= latestBlock)
            {
                _logger.LogDebug(
                    $"[EvmSearchWorker] Current: {consumedBlockHeight} Latest: {latestBlock}, Waiting for syncing {networkName} latest block info.");
                return;
            }

            _logger.LogInformation(
                $"[EvmSearchWorker] {networkName} Starting HTTP query from block {consumedBlockHeight} to latestBlock {latestBlock}");

            var from = consumedBlockHeight + 1;
            var pendingRequests = new List<EvmReceivedMessageDto>();

            _logger.LogInformation($"[EvmSearchWorker] {networkName} Processed blocks from {from} to {latestBlock}.");
            for (var curFrom = from; curFrom <= latestBlock; curFrom += EvmSubscribeConstants.SubscribeBlockStep)
            {
                var currentTo = Math.Min(curFrom + EvmSubscribeConstants.SubscribeBlockStep - 1, latestBlock);

                try
                {
                    var request = await _provider.GetEvmLogsAsync(web3, options.ContractAddress, curFrom, currentTo);
                    pendingRequests.AddRange(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"[EvmSearchWorker] {networkName} Error processing blocks {curFrom} to {currentTo}: {ex.Message}");
                    throw;
                }
            }

            await _provider.StartCrossChainRequestsFromEvm(pendingRequests);
            await SaveBlockHeightAsync(networkName, latestBlock);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmSearchWorker] {networkName} Error processing http subscribe.");
        }
    }

    private async Task SaveBlockHeightAsync(string networkName, long blockHeight)
    {
        await _provider.SaveConsumedHeightAsync(networkName, blockHeight);
        _heightMap[networkName] = blockHeight;
    }

    private async Task Initialize()
    {
        _logger.LogDebug("[EvmSearchWorker] Start consumption height setting");

        foreach (var op in _evmOptions.ContractConfig.Values)
        {
            var searchedHeight = await _provider.GetStartHeightAsync(op.NetworkName);

            _heightMap[op.NetworkName] = searchedHeight;
            _evmClientMap[op.NetworkName] = new Web3(op.Api);

            _logger.LogDebug($"[EvmSearchWorker] {op.NetworkName} has subscribed {searchedHeight}.");
        }
    }
}