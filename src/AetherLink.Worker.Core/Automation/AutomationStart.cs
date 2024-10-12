using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Automation;

public class AutomationStart : AsyncBackgroundJob<AutomationStartJobArgs>, ITransientDependency
{
    private readonly IObjectMapper _mapper;
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly ILogger<AutomationStart> _logger;
    private readonly ISchedulerService _schedulerService;
    private readonly ISignatureProvider _signatureProvider;
    private readonly IOracleContractProvider _contractProvider;

    public AutomationStart(IPeerManager peerManager, ILogger<AutomationStart> logger, IObjectMapper mapper,
        ISchedulerService schedulerService, IJobProvider jobProvider, IOracleContractProvider contractProvider,
        ISignatureProvider signatureProvider)
    {
        _logger = logger;
        _mapper = mapper;
        _peerManager = peerManager;
        _jobProvider = jobProvider;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
        _signatureProvider = signatureProvider;
    }


    public override async Task ExecuteAsync(AutomationStartJobArgs args)
    {
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(AutomationStart), MethodName = nameof(HandleException))]
    public virtual async Task Handler(AutomationStartJobArgs args)
    {
        var chainId = args.ChainId;
        var upkeepId = args.RequestId;
        var epoch = args.Epoch;
        var roundId = args.RoundId;
        var blockTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
        var context = new OCRContext
        {
            RequestId = upkeepId,
            ChainId = chainId,
            RoundId = roundId,
            Epoch = epoch
        };
        var id = AutomationHelper.GenerateCronUpkeepId(context);

        var job = await _jobProvider.GetAsync(args);
        if (job == null) job = _mapper.Map<AutomationStartJobArgs, JobDto>(args);
        else if (job.State == RequestState.RequestCanceled || epoch < job.Epoch) return;
        else if (job.RoundId == 0 && job.State == RequestState.RequestEnd)
            blockTime = DateTimeOffset.FromUnixTimeMilliseconds(job.TransactionBlockTime).DateTime;

        job.RequestReceiveTime = _schedulerService.UpdateBlockTime(blockTime);
        job.RoundId = roundId;
        job.State = RequestState.RequestStart;
        await _jobProvider.SetAsync(job);

        _logger.LogDebug($"[Automation] {id} startTime {args.StartTime}, blockTime {job.TransactionBlockTime}");

        var commitment = await _contractProvider.GetRequestCommitmentAsync(chainId, upkeepId);
        var payload = AutomationHelper.GetUpkeepPerformData(commitment);

        if (_peerManager.IsLeader(epoch, roundId))
        {
            _logger.LogInformation($"[Automation][Leader] {id} Is Leader.");
            var request = new QueryReportSignatureRequest { Context = context, Payload = payload };
            _signatureProvider.LeaderInitMultiSign(chainId, id, _signatureProvider.GenerateMsg(
                    await _contractProvider.GenerateTransmitDataAsync(chainId, upkeepId, epoch, payload))
                .ToByteArray());
            await _peerManager.BroadcastAsync(p => p.QueryReportSignatureAsync(request));
        }

        _schedulerService.StartCronUpkeepScheduler(job);

        _logger.LogInformation($"[Automation] {id} Waiting for request end.");
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex, AutomationJobArgs args)
    {
        var context = new OCRContext
        {
            RequestId = args.RequestId,
            ChainId = args.ChainId,
            RoundId = args.RoundId,
            Epoch = args.Epoch
        };
        var id = AutomationHelper.GenerateCronUpkeepId(context);
        _logger.LogError(ex, $"[Automation] {id} Start process failed.");

        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
        };
    }

    #endregion
}