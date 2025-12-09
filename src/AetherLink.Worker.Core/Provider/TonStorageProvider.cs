using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITonStorageProvider
{
    Task<TonIndexerDto> GetTonIndexerInfoAsync();
    Task<TonLatestBlockInfoDto> GetTonCenterLatestBlockInfoAsync();
    Task SetTonIndexerInfoAsync(TonIndexerDto tonIndexer);
    Task SetTonCenterLatestBlockInfoAsync(TonLatestBlockInfoDto tonIndexer);
}

public class TonStorageProvider : ITonStorageProvider, ISingletonDependency
{
    private readonly IStorageProvider _storageProvider;

    public TonStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    public async Task<TonIndexerDto> GetTonIndexerInfoAsync()
        => await _storageProvider.GetAsync<TonIndexerDto>(TonStringConstants.TonIndexerStorageKey);

    public async Task<TonLatestBlockInfoDto> GetTonCenterLatestBlockInfoAsync()
        => await _storageProvider.GetAsync<TonLatestBlockInfoDto>(TonStringConstants.TonCenterLatestBlockInfoKey);

    public async Task SetTonIndexerInfoAsync(TonIndexerDto tonIndexer)
        => await _storageProvider.SetAsync(TonStringConstants.TonIndexerStorageKey, tonIndexer);

    public async Task SetTonCenterLatestBlockInfoAsync(TonLatestBlockInfoDto tonIndexer)
        => await _storageProvider.SetAsync(TonStringConstants.TonCenterLatestBlockInfoKey, tonIndexer);
}