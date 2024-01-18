using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Hangfire;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RequestCancelProcessJob : AsyncBackgroundJob<RequestCancelProcessJobArgs>, ITransientDependency
{
    private readonly IRequestProvider _requestProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<RequestCancelProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;

    public RequestCancelProcessJob(IRecurringJobManager recurringJobManager, ILogger<RequestCancelProcessJob> logger,
        ISchedulerService schedulerService, IRequestProvider requestProvider)
    {
        _logger = logger;
        _requestProvider = requestProvider;
        _schedulerService = schedulerService;
        _recurringJobManager = recurringJobManager;
    }

    public override async Task ExecuteAsync(RequestCancelProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);
        try
        {
            _logger.LogInformation("[RequestCancelProcess] {name} Start cancel job timer.", argId);
            _recurringJobManager.RemoveIfExists(IdGeneratorHelper.GenerateId(chainId, reqId));

            var request = await _requestProvider.GetAsync(args);
            if (request == null)
            {
                _logger.LogWarning("[RequestCancelProcess] {name} not exist", args);
                return;
            }

            request.State = RequestState.RequestCanceled;
            await _requestProvider.SetAsync(request);

            _schedulerService.CancelAllSchedule(request);

            _logger.LogInformation("[RequestCancelProcess] {name} Cancel job timer finished.", argId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RequestCancelProcess] {name} Cancel job failed.", argId);
        }
    }
}