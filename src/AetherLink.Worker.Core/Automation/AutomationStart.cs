using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Common;
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
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId, args.Epoch, args.RoundId);
        var blockTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;

        try
        {
            var job = await _jobProvider.GetAsync(args);
            if (job == null) job = _mapper.Map<AutomationStartJobArgs, JobDto>(args);
            else if (job.State == RequestState.RequestCanceled || args.Epoch < job.Epoch) return;
            else if (job.RoundId == 0 && job.State == RequestState.RequestEnd)
                blockTime = DateTimeOffset.FromUnixTimeMilliseconds(job.TransactionBlockTime).DateTime;

            job.RequestReceiveTime = _schedulerService.UpdateBlockTime(blockTime);
            job.RoundId = args.RoundId;
            job.State = RequestState.RequestStart;
            await _jobProvider.SetAsync(job);

            _logger.LogDebug("[Automation] {name} start startTime {time}, blockTime {blockTime}", argId, args.StartTime,
                job.TransactionBlockTime);

            var commitment = await _contractProvider.GetRequestCommitmentAsync(args.ChainId, args.RequestId);
            var originInput = RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData);

            if (_peerManager.IsLeader(args.Epoch, args.RoundId))
            {
                _logger.LogInformation("[Automation][Leader] {name} Is Leader.", argId);
                var request = new QueryReportSignatureRequest
                {
                    Context = new()
                    {
                        RequestId = args.RequestId,
                        ChainId = args.ChainId,
                        RoundId = args.RoundId,
                        Epoch = args.Epoch
                    },
                    CheckData = originInput.PerformData
                };

                _signatureProvider.LeaderInitMultiSign(request.Context, _signatureProvider
                    .GenerateMsg(await _contractProvider.GenerateTransmitDataAsync(args.ChainId, args.RequestId,
                        args.Epoch, originInput.PerformData)).ToByteArray());
                await _peerManager.BroadcastAsync(p => p.QueryReportSignatureAsync(request));
            }

            _schedulerService.StartScheduler(job, SchedulerType.CheckRequestEndScheduler);

            _logger.LogInformation("[Automation] {name} Waiting for request end.", argId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation] {name} Start process failed.", argId);
        }
    }
}