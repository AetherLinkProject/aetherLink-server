using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
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
    private readonly ILogger<AutomationJobInit> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationJobInit(IRecurringJobManager recurringJobManager, ILogger<AutomationJobInit> logger,
        IOracleContractProvider oracleContractProvider, RetryProvider retryProvider)
    {
        _logger = logger;
        _retryProvider = retryProvider;
        _recurringJobManager = recurringJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(AutomationJobArgs args)
    {
        var chainId = args.ChainId;
        var requestId = args.RequestId;
        var argId = IdGeneratorHelper.GenerateId(chainId, requestId);

        try
        {
            _logger.LogInformation($"[Automation] Get a new upkeep {argId} at blockHeight:{args.BlockHeight}.");

            var triggerDataStr = RegisterUpkeepInput.Parser.ParseFrom(
                    (await _oracleContractProvider.GetRequestCommitmentAsync(chainId, requestId)).SpecificData)
                .TriggerData.ToStringUtf8();
            var triggerData = JsonConvert.DeserializeObject<AutomationTriggerDataDto>(triggerDataStr);
            if (triggerData == null)
            {
                _logger.LogWarning($"[Automation] {argId} Invalid upkeep trigger spec: {triggerDataStr}.");
                return;
            }

            args.JobSpec = triggerDataStr;
            args.Epoch = await _oracleContractProvider.GetStartEpochAsync(args.ChainId, args.BlockHeight);

            switch (triggerData.TriggerDataSpec.TriggerType)
            {
                case TriggerType.Cron:
                    _logger.LogInformation("[Automation] {name} Start a automation timer.", argId);
                    _recurringJobManager.AddOrUpdate<AutomationTimerProvider>(
                        IdGeneratorHelper.GenerateId(chainId, requestId), timer => timer.ExecuteAsync(args),
                        () => triggerData.Cron);

                    break;
                case TriggerType.Log:
                default:
                    throw new NotImplementedException();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[Automation] Get Automation job info {name} timeout, retry later.", argId);
            await _retryProvider.RetryAsync(args, untilFailed: true, backOff: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation] Get a new Automation job {name} failed.", argId);
        }
    }
}