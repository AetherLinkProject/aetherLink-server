using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using NUglify.Helpers;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITonStorageProvider
{
    Task<TonIndexerDto> GetTonIndexerInfo();
    Task SetTonIndexerInfo(TonIndexerDto tonIndexer);
}

public class TonStorageProvider:ITonStorageProvider,ISingletonDependency
{
    private readonly IStorageProvider _storageProvider;

    public TonStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }
    
    #region TonIndexer
    public async Task<TonIndexerDto> GetTonIndexerInfo()
    {
        return await _storageProvider.GetAsync<TonIndexerDto>(TonStringConstants.TonIndexerStorageKey) ?? new TonIndexerDto();;
    }

    public async Task SetTonIndexerInfo(TonIndexerDto tonIndexer)
    {
        await _storageProvider.SetAsync(TonStringConstants.TonIndexerStorageKey, tonIndexer);
    }
    
    #endregion
}