using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AetherLink.Worker.Core.Worker;

public class TonChainStatesWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly TonChainStatesOptions _options;
    private readonly ITonStorageProvider _storageProvider;
    private readonly ILogger<TonChainStatesWorker> _logger;
    private readonly ITonCenterApiProvider _tonCenterApiProvider;

    public TonChainStatesWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<TonChainStatesOptions> options, ILogger<TonChainStatesWorker> logger,
        ITonCenterApiProvider tonCenterApiProvider, ITonStorageProvider storageProvider) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _storageProvider = storageProvider;
        timer.Period = _options.StatesCheckInterval;
        _tonCenterApiProvider = tonCenterApiProvider;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[TonChainStatesWorker] Start checking Ton chain state...");

        var masterChainInfo = await _tonCenterApiProvider.GetCurrentHighestBlockHeightAsync();
        if (masterChainInfo.Last == null)
        {
            _logger.LogError("[TonChainStatesWorker] Failed to get the Ton master chain block states.");
            return;
        }

        var masterChainBlock = masterChainInfo.Last;
        if (masterChainBlock.Workchain != -1 || masterChainBlock.Shard != "8000000000000000")
        {
            _logger.LogWarning("[TonChainStatesWorker]Failed to retrieve the Ton master chain block height.");
            return;
        }

        await _storageProvider.SetTonCenterLatestBlockInfoAsync(new()
        {
            Shard = masterChainBlock.Shard,
            McBlockSeqno = masterChainBlock.MasterchainBlockRef.Seqno,
            StartLt = masterChainBlock.StartLt,
            EndLt = masterChainBlock.EndLt
        });

        _logger.LogInformation(
            $"[TonChainStatesWorker]Set Ton master chain block states {masterChainBlock.MasterchainBlockRef.Seqno} successful.");
    }
}