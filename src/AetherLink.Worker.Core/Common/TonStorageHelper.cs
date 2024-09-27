using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;

namespace AetherLink.Worker.Core.Common;


public sealed partial class TonHelper
{
    private readonly IStorageProvider _storageProvider;
    private const string TonIndexerStorageKey = "TonIndexer";
    private const string TonTaskStorageKey = "TonTask";
    
    #region TonIndexer
    public async Task<TonIndexerDto> GetTonIndexerFromStorage()
    {
        var result = await _storageProvider.GetAsync<TonIndexerDto>(TonIndexerStorageKey);
        if (result == null)
        {
            result = new TonIndexerDto();
        }

        return result;
    }

    public async Task StorageTonIndexer(TonIndexerDto tonIndexer)
    {
        await _storageProvider.SetAsync(TonIndexerStorageKey, tonIndexer);
    }
    
    #endregion

    #region Ton task

    public async Task StorageTonTask(string messageId, TonChainTaskDto task)
    {
        await _storageProvider.SetHashsetAsync(TonTaskStorageKey, messageId, task);
    }

    public async Task<TonChainTaskDto> GetTonTask(string messageId)
    {
       return await _storageProvider.GetHashsetFieldAsync<TonChainTaskDto>(TonTaskStorageKey, messageId);
    }

    public async Task<List<TonChainTaskDto>> GetAllTonTask()
    {
        var result = new List<TonChainTaskDto>();
        var perPageCount = 20;
        var count = 0;
        while (true)
        {
             var dic = await _storageProvider.ScanHashset<TonChainTaskDto>(TonIndexerStorageKey, "", perPageCount * count,
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