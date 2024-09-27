using System;
using System.Threading.Tasks;
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
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IRampMessageProvider _messageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<RampRequestPartialSignatureJob> _logger;

    public RampRequestPartialSignatureJob(ILogger<RampRequestPartialSignatureJob> logger,
        IRampMessageProvider messageProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
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
            var messageData = await _messageProvider.GetAsync(args.ChainId, args.MessageId);
            if (messageData == null || args.RoundId > messageData.RoundId)
            {
                _logger.LogDebug($"The Ramp request {args.MessageId} from leader is not ready now,will try it later.");
                // todo: retry
            }
            else if (args.RoundId < messageData.RoundId)
            {
                _logger.LogWarning($"The Ramp request {args.MessageId} from leader is too old.");
                return;
            }

            // todo: partial signature
            var partialSig = new byte[] { 12, 34, 56 };

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
            Console.WriteLine(e);
            throw;
        }
    }
}