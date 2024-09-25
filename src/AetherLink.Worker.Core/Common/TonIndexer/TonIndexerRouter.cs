using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerRouter:ISingletonDependency
{
    private readonly List<TonIndexerWrapper> _indexerList;
    private readonly ILogger<TonIndexerRouter> _logger;

    public TonIndexerRouter(IEnumerable<TonIndexerBase> tonIndexers, ILogger<TonIndexerRouter> logger)
    {
        _logger = logger;
        
        var tonIndexerList = new List<TonIndexerWrapper>();
        foreach (var item in tonIndexers)
        {
            tonIndexerList.Add(new TonIndexerWrapper(item));
        }
    
        tonIndexerList.Sort((s1,s2)=> s1.IndexerBase.Weight.CompareTo(s2.IndexerBase.Weight));

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

