using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class TransmittedEventProcessJob : AsyncBackgroundJob<TransmittedEventProcessJobArgs>, ISingletonDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<TransmittedEventProcessJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public TransmittedEventProcessJob(ISchedulerService schedulerService, ILogger<TransmittedEventProcessJob> logger,
        IOracleContractProvider oracleContractProvider, IJobProvider jobProvider, IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _storageProvider = storageProvider;
        _schedulerService = schedulerService;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(TransmittedEventProcessJobArgs args)
    {
        var chainId = args.ChainId;
        var requestId = args.RequestId;
        var argId = IdGeneratorHelper.GenerateId(chainId, requestId);
        try
        {
            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, requestId);
            if (commitment.RequestTypeIndex == RequestTypeConst.Automation)
            {
                if (AutomationHelper.GetTriggerType(commitment) == TriggerType.Log)
                {
                    var report =
                        await _oracleContractProvider.GetTransmitReportByTransactionIdAsync(chainId,
                            args.TransactionId);
                    var payload = LogTriggerCheckData.Parser.ParseFrom(report.Result);
                    var triggerKey =
                        AutomationHelper.GetLogTriggerKeyByPayload(chainId, requestId, payload.ToByteArray());
                    var logTriggerInfo = await _storageProvider.GetAsync<LogTriggerDto>(triggerKey);
                    if (logTriggerInfo != null) _schedulerService.CancelLogUpkeep(logTriggerInfo);
                    return;
                }
            }

            var job = await _jobProvider.GetAsync(args);
            if (job == null)
            {
                _logger.LogDebug("[Transmitted] {name} not need update.", argId);
                return;
            }

            if (commitment.RequestTypeIndex == RequestTypeConst.Automation) _schedulerService.CancelCronUpkeep(job);
            else _schedulerService.CancelAllSchedule(job);

            _logger.LogInformation("[Transmitted] {name} epoch:{epoch} end", argId, job.Epoch);

            job.TransactionBlockTime = args.StartTime;
            job.State = RequestState.RequestEnd;
            job.RoundId = 0;
            job.Epoch = args.Epoch;
            await _jobProvider.SetAsync(job);

            _logger.LogDebug("[Transmitted] {name} will update epoch => {epoch}, block-time => {time}", argId,
                args.Epoch, args.StartTime);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Transmitted] {name} Transmitted process failed.", argId);
        }
    }
}