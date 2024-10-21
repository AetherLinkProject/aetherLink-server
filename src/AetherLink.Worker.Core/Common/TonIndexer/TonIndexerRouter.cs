using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using TonSdk.Core.Boc;
using Volo.Abp.DependencyInjection;
using TonSdk.Core;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerRouter:ISingletonDependency
{
    private readonly List<TonIndexerWrapper> _indexerList;
    private readonly ILogger<TonIndexerRouter> _logger;

    public TonIndexerRouter(IEnumerable<ITonIndexerProvider> tonIndexers, ILogger<TonIndexerRouter> logger)
    {
        _logger = logger;
        
        var tonIndexerList = new List<TonIndexerWrapper>();
        foreach (var item in tonIndexers)
        {
            tonIndexerList.Add(new TonIndexerWrapper(item));
        }
    
        tonIndexerList.Sort((s1,s2)=> s2.IndexerBase.Weight.CompareTo(s1.IndexerBase.Weight));

        _indexerList = tonIndexerList;
    }

    public async Task<(List<CrossChainToTonTransactionDto>, TonIndexerDto)> GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
    {
        foreach (var item in _indexerList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetSubsequentTransaction(tonIndexerDto);
                if (success)
                {
                    return result;
                }
            }
        }    
        
        _logger.LogError($"All ton indexer provider are disabled");
        return (null,null);
    }

    [ItemCanBeNull]
    public async Task<string> CommitTransaction(Cell signedBodyCell)
    {
        foreach (var item in _indexerList)
        {
            if (await item.CheckAvailable())
            {
                _logger.LogDebug($"[Ton Send Transaction] Start Provider is:{item.IndexerBase.ApiProviderName}");
                var (success, result) = await item.CommitTransaction(signedBodyCell);
                if (success)
                {
                    _logger.LogDebug($"[Ton Send Transaction] End");
                    return result;
                }
            }
        }
        
        _logger.LogError($"All ton indexer provider are disabled");
        return null;
    }

    public virtual async Task<uint?> GetAddressSeqno(Address address)
    {
        foreach (var item in _indexerList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetAddressSeqno(address);
                if (success)
                {
                    return result;
                }
            }
        }
        
        _logger.LogError($"All ton indexer provider are disabled");
        return null;
    }

    public async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        foreach (var item in _indexerList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetTransactionInfo(txId);
                if (success)
                {
                    return result;
                }
            }
        }

        _logger.LogError($"All ton indexer provider are disabled");
        return null;
    }

    public List<TonIndexerWrapper> GetIndexerApiProviderList()
    {
        var result = new List<TonIndexerWrapper>();
        foreach (var item in _indexerList)
        {
            result.Add(item);
        }

        return result;
    }
}

