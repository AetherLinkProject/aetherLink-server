using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITonStorageProvider
{
    Task<TonIndexerDto> GetTonIndexerInfo();
    Task SetTonIndexerInfo(TonIndexerDto tonIndexer);
    Task SetTonTask(string messageId, TonChainTaskDto task);
    Task DeleteTonTask(string messageId);
    Task<TonChainTaskDto> GetTonTask(string messageId);
    Task<List<TonChainTaskDto>> GetAllTonTask();
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
        var result = await _storageProvider.GetAsync<TonIndexerDto>(TonStringConstants.TonIndexerStorageKey) ?? new TonIndexerDto();

        return result;
    }

    public async Task SetTonIndexerInfo(TonIndexerDto tonIndexer)
    {
        await _storageProvider.SetAsync(TonStringConstants.TonIndexerStorageKey, tonIndexer);
    }
    
    #endregion

    #region Ton task

    public async Task SetTonTask(string messageId, TonChainTaskDto task)
    {
        await _storageProvider.SetHashsetAsync(TonStringConstants.TonTaskStorageKey, messageId, task);
    }

    public async Task DeleteTonTask(string messageId)
    {
        await _storageProvider.DeleteHashsetFieldAsync(TonStringConstants.TonTaskStorageKey, messageId);
    }

    [ItemCanBeNull]
    public async Task<TonChainTaskDto> GetTonTask(string messageId)
    {
        return await _storageProvider.GetHashsetFieldAsync<TonChainTaskDto>(TonStringConstants.TonTaskStorageKey, messageId);
    }

    public async Task<List<TonChainTaskDto>> GetAllTonTask()
    {
        var result = new List<TonChainTaskDto>();
        var perPageCount = 20;
        var count = 0;
        while (true)
        {
            var dic = await _storageProvider.ScanHashset<TonChainTaskDto>(TonStringConstants.TonTaskStorageKey, "", perPageCount * count,
                perPageCount);
            foreach (var (_, value) in dic)
            {
                result.Add(value);
            }

            if (dic.Count == 0)
            {
                break;
            }
        }

        return result;
    }

    #endregion
}