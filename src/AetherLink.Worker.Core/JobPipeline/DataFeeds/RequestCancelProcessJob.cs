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
using Hangfire;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline.DataFeeds;

public class RequestCancelProcessJob : AsyncBackgroundJob<RequestCancelProcessJobArgs>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IFilterStorage _filterStorage;
    private readonly IStorageProvider _storageProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestCancelProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public RequestCancelProcessJob(IRecurringJobManager recurringJobManager, ILogger<RequestCancelProcessJob> logger,
        ISchedulerService schedulerService, IJobProvider jobProvider, IOracleContractProvider oracleContractProvider,
        IFilterStorage filterStorage, IStorageProvider storageProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _filterStorage = filterStorage;
        _storageProvider = storageProvider;
        _schedulerService = schedulerService;
        _recurringJobManager = recurringJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(RequestCancelProcessJobArgs args)
    {
        var chainId = args.ChainId;
        var requestId = args.RequestId;
        try
        {
            _logger.LogInformation($"[RequestCancelProcess] {chainId} {requestId} Start cancel job.");

            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, requestId);
            if (commitment.RequestTypeIndex == RequestTypeConst.Automation &&
                AutomationHelper.GetTriggerType(commitment) == TriggerType.Log)
            {
                var logUpkeepInfoId = IdGeneratorHelper.GenerateUpkeepInfoId(chainId, requestId);
                var logUpkeepInfo = await _storageProvider.GetAsync<LogUpkeepInfoDto>(logUpkeepInfoId);
                _schedulerService.CancelLogUpkeepAllSchedule(logUpkeepInfo);

                await _filterStorage.DeleteFilterAsync(logUpkeepInfo);
                await _storageProvider.RemoveAsync(logUpkeepInfoId);
                return;
            }

            _recurringJobManager.RemoveIfExists(IdGeneratorHelper.GenerateId(chainId, requestId));
            var job = await _jobProvider.GetAsync(args);
            if (job == null)
            {
                _logger.LogInformation($"[RequestCancelProcess] {chainId} {requestId} not exist");
                return;
            }

            _schedulerService.CancelAllSchedule(job);

            job.State = RequestState.RequestCanceled;
            await _jobProvider.SetAsync(job);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[RequestCancelProcess] {chainId} {requestId} Cancel job failed.");
        }
        finally
        {
            // todo: add state cleanup
            _logger.LogInformation($"[RequestCancelProcess] {chainId} {requestId} Cancel job finished.");
        }
    }
}