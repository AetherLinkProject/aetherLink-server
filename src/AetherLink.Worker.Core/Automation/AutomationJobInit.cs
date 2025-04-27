using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Provider;
using Hangfire;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationJobInit : AsyncBackgroundJob<AutomationJobArgs>, ITransientDependency
{
    private readonly RetryProvider _retryProvider;
    private readonly IFilterStorage _filterStorage;
    private readonly IRecurringJobManager _jobManager;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<AutomationJobInit> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationJobInit(IRecurringJobManager jobManager, ILogger<AutomationJobInit> logger,
        IOracleContractProvider oracleContractProvider, RetryProvider retryProvider, IFilterStorage filterStorage,
        IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobManager = jobManager;
        _retryProvider = retryProvider;
        _filterStorage = filterStorage;
        _storageProvider = storageProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(AutomationJobArgs args)
    {
        var chainId = args.ChainId;
        var upkeepId = args.RequestId;
        var jobId = IdGeneratorHelper.GenerateId(chainId, upkeepId);

        try
        {
            _logger.LogInformation($"[Automation] Get a new upkeep {jobId} at blockHeight:{args.BlockHeight}.");
            var commitment = await _oracleContractProvider.GetRequestCommitmentByTxAsync(chainId, args.TransactionId);
            var triggerDataStr = AutomationHelper.GetTriggerData(commitment);

            _logger.LogDebug($"Get automation job spec: {triggerDataStr}");
            switch (AutomationHelper.GetTriggerType(commitment))
            {
                case TriggerType.Cron:
                    _logger.LogInformation($"[Automation] {jobId} Start a automation timer.");
                    await AddCronUpkeepAsync(args, jobId, triggerDataStr);
                    break;
                case TriggerType.Log:
                    _logger.LogInformation($"[Automation] {jobId} Start a automation log trigger.");
                    var upkeepAddress = AutomationHelper.GetUpkeepAddress(commitment);
                    await AddLogUpkeepAsync(chainId, upkeepId, upkeepAddress, triggerDataStr);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[Automation] Get Automation job info {name} timeout, retry later.", jobId);
            await _retryProvider.RetryAsync(args, untilFailed: true, backOff: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation] Get a new Automation job {name} failed.", jobId);
        }
    }

    private async Task AddCronUpkeepAsync(AutomationJobArgs args, string jobId, string triggerSpec)
    {
        var triggerData = JsonConvert.DeserializeObject<CronTriggerDataDto>(triggerSpec);
        if (triggerData == null)
        {
            _logger.LogWarning($"[Automation] Invalid cron trigger upkeep spec: {triggerSpec}.");
            return;
        }

        args.JobSpec = triggerSpec;
        args.Epoch = await _oracleContractProvider.GetStartEpochAsync(args.ChainId, args.BlockHeight);
        _jobManager.AddOrUpdate<AutomationTimerProvider>(jobId, t => t.ExecuteAsync(args), () => triggerData.Cron);
    }

    private async Task AddLogUpkeepAsync(string chainId, string upkeepId, string upkeepAddress, string triggerSpec)
    {
        var logTriggerData = JsonConvert.DeserializeObject<LogTriggerDataSpecDto>(triggerSpec);
        if (logTriggerData == null)
        {
            _logger.LogWarning($"[Automation] Invalid log trigger upkeep spec: {triggerSpec}.");
            return;
        }

        var upkeepInfo = new LogUpkeepInfoDto
        {
            ChainId = chainId,
            UpkeepId = upkeepId,
            UpkeepAddress = upkeepAddress,
            TriggerChainId = logTriggerData.ChainId,
            TriggerEventName = logTriggerData.EventName,
            TriggerContractAddress = logTriggerData.ContractAddress
        };

        _logger.LogDebug("Starting save upkeep info in storage");
        await _storageProvider.SetAsync(IdGeneratorHelper.GenerateUpkeepInfoId(chainId, upkeepId), upkeepInfo);
        await _filterStorage.AddFilterAsync(upkeepInfo);
    }
}