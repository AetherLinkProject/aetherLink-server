using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Consts;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Hangfire;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RequestCancelProcessJob : AsyncBackgroundJob<RequestCancelProcessJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly ISchedulerService _schedulerService;
    private readonly IJobRequestProvider _jobRequestProvider;
    private readonly ILogger<RequestCancelProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;

    public RequestCancelProcessJob(IRecurringJobManager recurringJobManager, ILogger<RequestCancelProcessJob> logger,
        IPeerManager peerManager, ISchedulerService schedulerService, IJobRequestProvider jobRequestProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _schedulerService = schedulerService;
        _jobRequestProvider = jobRequestProvider;
        _recurringJobManager = recurringJobManager;
    }

    public override async Task ExecuteAsync(RequestCancelProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId);
        _logger.LogInformation("[RequestCancelProcess] {name} Cancel job timer.", argsName);
        _recurringJobManager.RemoveIfExists(IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId));

        var epoch = await _peerManager.GetEpochAsync(chainId);
        var request = await _jobRequestProvider.GetJobRequestAsync(chainId, reqId, epoch);

        if (request == null)
        {
            _logger.LogWarning("[RequestCancelProcess] {name} not exist", args);
            return;
        }

        request.State = RequestState.RequestCanceled;
        request.RequestCanceledTime = DateTime.UtcNow;
        await _jobRequestProvider.SetJobRequestAsync(request);

        foreach (SchedulerType schedulerType in Enum.GetValues(typeof(SchedulerType)))
        {
            _schedulerService.CancelScheduler(request, schedulerType);
        }

        _logger.LogInformation("[RequestCancelProcess] {name} Cancel job timer finished.", argsName);
    }
}