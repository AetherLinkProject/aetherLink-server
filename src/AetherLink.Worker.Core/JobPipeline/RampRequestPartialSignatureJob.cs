using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestPartialSignatureJob : AsyncBackgroundJob<RampRequestPartialSignatureJobArgs>,
    ITransientDependency
{
    private readonly TonHelper _tonHelper;
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IRetryProvider _retryProvider;
    private readonly IRampMessageProvider _messageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<RampRequestPartialSignatureJob> _logger;

    public RampRequestPartialSignatureJob(ILogger<RampRequestPartialSignatureJob> logger,
        IRampMessageProvider messageProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager,
        IObjectMapper objectMapper, TonHelper tonHelper, IRetryProvider retryProvider)
    {
        _logger = logger;
        _tonHelper = tonHelper;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _messageProvider = messageProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RampRequestPartialSignatureJobArgs args)
    {
        var messageId = args.MessageId;
        var epoch = args.Epoch;
        var roundId = args.RoundId;
        try
        {
            _logger.LogDebug(
                $"get leader ramp request partial signature request.{args.MessageId} {args.ChainId} {args.Epoch} {args.RoundId}");

            var messageData = await _messageProvider.GetAsync(args.MessageId);
            if (messageData == null || args.RoundId > messageData.RoundId)
            {
                await _retryProvider.RetryWithIdAsync(args,
                    IdGeneratorHelper.GenerateId(args.ChainId, args.MessageId, args.RoundId), backOff: true);

                _logger.LogDebug($"The Ramp request {args.MessageId} from leader is not ready now,will try it later.");
                await _retryProvider.RetryWithIdAsync(args, IdGeneratorHelper.GenerateId(messageId, epoch, roundId));
                return;
            }

            if (messageData.State == RampRequestState.RequestCanceled)
            {
                _logger.LogWarning($"Ramp request {args.MessageId} canceled");
                return;
            }

            if (args.RoundId < messageData.RoundId)
            {
                _logger.LogWarning($"The Ramp request {args.MessageId} from leader is too old.");
                return;
            }

            var partialSig = _tonHelper.ConsensusSignature(new()
            {
                MessageId = messageData.MessageId,
                SourceChainId = messageData.SourceChainId,
                TargetChainId = messageData.TargetChainId,
                Sender = messageData.Sender,
                Receiver = messageData.Receiver,
                Message = messageData.Data
            });

            // send partial signature to leader
            var nodeIndex = _peerManager.GetOwnIndex();
            if (_peerManager.IsLeader(args.Epoch, args.RoundId))
            {
                _logger.LogInformation($"[Ramp][Leader] {messageId}-{epoch} Insert partialSign in queue");

                var procJob =
                    _objectMapper.Map<RampRequestPartialSignatureJobArgs, RampRequestMultiSignatureJobArgs>(args);
                procJob.Signature = partialSig;
                procJob.Index = nodeIndex;
                await _backgroundJobManager.EnqueueAsync(procJob, BackgroundJobPriority.High);
                return;
            }

            var reportSign = _objectMapper.Map<RampRequestPartialSignatureJobArgs, ReturnPartialSignatureResults>(args);
            reportSign.Signature = ByteString.CopyFrom(partialSig);
            reportSign.Index = nodeIndex;

            await _peerManager.CommitToLeaderAsync(p => p.ReturnPartialSignatureResultsAsync(reportSign), epoch,
                roundId);

            _logger.LogInformation(
                $"[Ramp][Follower] {messageId}-{epoch} Send signature to leader, Waiting for transmitted.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Ramp]Partial signature failed.");
        }
    }
}