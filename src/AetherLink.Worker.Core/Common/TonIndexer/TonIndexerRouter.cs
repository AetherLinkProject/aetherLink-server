using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TonSdk.Core.Boc;
using Volo.Abp.DependencyInjection;
using TonSdk.Core;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerRouter:ISingletonDependency
{
    private readonly TonPublicConfigOptions _tonPublicConfigOptions;
    private readonly List<TonIndexerWrapper> _providerList;
    private readonly List<TonIndexerWrapper> _indexerProviderList;
    private readonly List<TonIndexerWrapper> _commitProviderList;
    private readonly ILogger<TonIndexerRouter> _logger;

    public TonIndexerRouter(IOptionsSnapshot<TonPublicConfigOptions> tonPublicOptions, IEnumerable<ITonIndexerProvider> tonIndexers, ILogger<TonIndexerRouter> logger)
    {
        _logger = logger;
        _tonPublicConfigOptions = tonPublicOptions.Value;
        var providerList = new List<TonIndexerWrapper>();
        var indexerList = new List<TonIndexerWrapper>();
        var commitList = new List<TonIndexerWrapper>();
        foreach (var item in tonIndexers)
        {
            var indexerWrapper = new TonIndexerWrapper(item);
            providerList.Add(indexerWrapper);
            if (_tonPublicConfigOptions.IndexerProvider.Contains(item.ApiProviderName))
            {
                indexerList.Add(indexerWrapper);
            }

            if (_tonPublicConfigOptions.CommitProvider.Contains(item.ApiProviderName))
            {
                commitList.Add(indexerWrapper);
            }
        }
        
        commitList.Sort((s1,s2)=> s2.IndexerBase.Weight.CompareTo(s1.IndexerBase.Weight));
        indexerList.Sort((s1,s2)=> s2.IndexerBase.Weight.CompareTo(s1.IndexerBase.Weight));
        providerList.Sort((s1,s2)=> s2.IndexerBase.Weight.CompareTo(s1.IndexerBase.Weight));
        
        _providerList = providerList;
        _indexerProviderList = indexerList;
        _commitProviderList = commitList;
    }

    public async Task<(List<CrossChainToTonTransactionDto>, TonIndexerDto)> GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
    {
        foreach (var item in _indexerProviderList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetSubsequentTransaction(tonIndexerDto);
                if (success)
                {
                    return result;
                }
                
                _logger.LogInformation($"Ton provider is changed,current indexer provider is:{item.IndexerBase.ApiProviderName}");
            }
        }    
        
        _logger.LogError($"All ton indexer provider are disabled");
        return (null,null);
    }

    [ItemCanBeNull]
    public async Task<string> CommitTransaction(Cell signedBodyCell)
    {
        foreach (var item in _commitProviderList)
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
                
                _logger.LogInformation($"Ton provider is changed,current commit provider is:{item.IndexerBase.ApiProviderName}");
            }
        }
        
        _logger.LogError($"All ton commit provider are disabled");
        return null;
    }

    public virtual async Task<uint?> GetAddressSeqno(Address address)
    {
        foreach (var item in _indexerProviderList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetAddressSeqno(address);
                if (success)
                {
                    return result;
                }
                
                _logger.LogInformation($"Ton provider is changed,current index provider is:{item.IndexerBase.ApiProviderName}");
            }
        }
        
        _logger.LogError($"All ton indexer provider are disabled");
        return null;
    }

    public async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        foreach (var item in _indexerProviderList)
        {
            if (await item.CheckAvailable())
            {
                var (success, result) = await item.GetTransactionInfo(txId);
                if (success)
                {
                    return result;
                }
                
                _logger.LogInformation($"Ton provider is changed,current index provider is:{item.IndexerBase.ApiProviderName}");
            }
        }

        _logger.LogError($"All ton indexer provider are disabled");
        return null;
    }

    public List<TonIndexerWrapper> GetIndexerApiProviderList()
    {
        var result = new List<TonIndexerWrapper>();
        foreach (var item in _indexerProviderList)
        {
            result.Add(item);
        }

        return result;
    }
}

