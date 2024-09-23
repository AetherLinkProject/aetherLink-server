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

    public TonIndexerRouter(IEnumerable<ITonIndexer> tonIndexers, ILogger<TonIndexerRouter> logger)
    {
        _logger = logger;
        
        var tonIndexerList = new List<TonIndexerWrapper>();
        foreach (var item in tonIndexers)
        {
            tonIndexerList.Add(new TonIndexerWrapper(item));
        }
    
        tonIndexerList.Sort((s1,s2)=> s1.Indexer.ApiWeight.CompareTo(s2.Indexer.ApiWeight));

        _indexerList = tonIndexerList;
    }

    public async Task<TransactionAnalysisDto<CrossChainToTonTransactionDto, TonIndexerDto>> GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
    {
        foreach (var item in _indexerList)
        {
            if (item.IsAvailable && await item.TryGetRequestAccess())
            {
                var (success, result) = await item.GetSubsequentTransaction(tonIndexerDto);
                if (success)
                {
                    return result;
                }
            }
        }    
        
        _logger.LogError($"All ton indexer are disabled");
        return null;
    }

    public async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        foreach (var item in _indexerList)
        {
            if (item.IsAvailable && await item.TryGetRequestAccess())
            {
                var (success, result) = await item.GetTransactionInfo(txId);
                if (success)
                {
                    return result;
                }
            }
        }

        _logger.LogError($"All ton indexer are disabled");
        return null;
    }
}

