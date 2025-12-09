using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider;

public interface IEvmIndexerProvider
{
    Task<long> GetLatestBlockHeightAsync(Web3 web3);
    Task<List<FilterLog>> GetEvmLogsAsync(Web3 web3, string contractAddress, long from, long to);
    Task<BlockWithTransactions> GetBlockByNumberAsync(Web3 web3, long blockNumber);
}

public class EvmIndexerProvider : IEvmIndexerProvider, ITransientDependency
{
    private readonly ILogger<EvmIndexerProvider> _logger;

    public EvmIndexerProvider(ILogger<EvmIndexerProvider> logger)
    {
        _logger = logger;
    }

    public async Task<long> GetLatestBlockHeightAsync(Web3 web3)
    {
        try
        {
            return (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[EvmSearchWorkerProvider] Get block number failed.");
            throw;
        }
    }

    public async Task<List<FilterLog>> GetEvmLogsAsync(Web3 web3, string contractAddress,
        long fromBlockHeight, long toBlockHeight)
    {
        _logger.LogDebug(
            $"[EvmSearchWorkerProvider] Search {contractAddress} blocks from {fromBlockHeight} to {toBlockHeight}.");
        var filterInput = new NewFilterInput
        {
            FromBlock = new BlockParameter((ulong)fromBlockHeight),
            ToBlock = new BlockParameter((ulong)toBlockHeight),
            Address = new[] { contractAddress }
        };

        try
        {
            var logs = await web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
            return logs.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[EvmSearchWorkerProvider] Search {contractAddress} blocks from {fromBlockHeight} to {toBlockHeight} failed.");
            throw;
        }
    }

    public async Task<BlockWithTransactions> GetBlockByNumberAsync(Web3 web3, long blockNumber)
    {
        try
        {
            var blockParameter = new BlockParameter((ulong)blockNumber);
            return await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockParameter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[EvmSearchWorkerProvider] Get block {blockNumber} failed.");
            throw;
        }
    }
}