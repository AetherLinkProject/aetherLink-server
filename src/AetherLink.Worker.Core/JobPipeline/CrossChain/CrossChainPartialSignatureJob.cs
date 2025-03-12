using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainKeyring;
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

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainPartialSignatureJob : AsyncBackgroundJob<CrossChainPartialSignatureJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IRetryProvider _retryProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CrossChainPartialSignatureJob> _logger;
    private readonly Dictionary<long, IChainKeyring> _offChainKeyring;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainPartialSignatureJob(ILogger<CrossChainPartialSignatureJob> logger, IObjectMapper objectMapper,
        ICrossChainRequestProvider crossChainRequestProvider, IRetryProvider retryProvider, IPeerManager peerManager,
        IBackgroundJobManager backgroundJobManager, IEnumerable<IChainKeyring> offChainKeyring)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _backgroundJobManager = backgroundJobManager;
        _crossChainRequestProvider = crossChainRequestProvider;
        _offChainKeyring = offChainKeyring.GroupBy(x => x.ChainId).Select(g => g.First())
            .ToDictionary(x => x.ChainId, y => y);
    }

    public override async Task ExecuteAsync(CrossChainPartialSignatureJobArgs args)
    {
        var reportContext = args.ReportContext;
        var messageId = reportContext.MessageId;
        var epoch = reportContext.Epoch;
        var roundId = reportContext.RoundId;
        _logger.LogDebug(
            $"[CrossChain] Get leader partial signature request, {messageId} {reportContext.SourceChainId} to {reportContext.TargetChainId} {epoch} {roundId}");
        try
        {
            var crossChainData = await _crossChainRequestProvider.GetAsync(messageId);
            if (crossChainData == null || roundId > crossChainData.ReportContext.RoundId)
            {
                await _retryProvider.RetryWithIdAsync(args,
                    IdGeneratorHelper.GenerateId(messageId, roundId), backOff: true);

                _logger.LogDebug(
                    $"[CrossChain] The request {messageId} from leader is not ready now, will try it later.");
                return;
            }

            if (crossChainData.State == CrossChainState.RequestCanceled)
            {
                _logger.LogWarning($"[CrossChain] The request {messageId} canceled");
                return;
            }

            if (roundId < crossChainData.ReportContext.RoundId)
            {
                _logger.LogWarning($"[CrossChain] The request {messageId} from leader is too old.");
                return;
            }

            if (!_offChainKeyring.TryGetValue(reportContext.TargetChainId, out var signer))
            {
                _logger.LogWarning($"[CrossChain] Unknown target chain id: {reportContext.TargetChainId}");
                return;
            }

            // todo: implement aelf and ton
            var crossChainReport = new CrossChainReportDto
            {
                Message = crossChainData.Message,
                TokenTransferMetadataDto = crossChainData.TokenTransferMetadataDto
            };
            var partialSig = signer.OffChainSign(reportContext, crossChainReport);

            // send partial signature to leader
            var nodeIndex = _peerManager.GetOwnIndex();
            if (_peerManager.IsLeader(epoch, roundId))
            {
                _logger.LogInformation(
                    $"[CrossChain][Leader] {messageId}-{epoch} Insert partialSign in queue");

                var procJob =
                    _objectMapper.Map<CrossChainPartialSignatureJobArgs, CrossChainMultiSignatureJobArgs>(args);
                procJob.Signature = partialSig;
                procJob.Index = nodeIndex;
                await _backgroundJobManager.EnqueueAsync(procJob, BackgroundJobPriority.High);
                return;
            }

            var reportSign = _objectMapper.Map<CrossChainPartialSignatureJobArgs, ReturnPartialSignatureResults>(args);
            reportSign.Signature = ByteString.CopyFrom(partialSig);
            reportSign.Index = nodeIndex;

            await _peerManager.CommitToLeaderAsync(p => p.ReturnPartialSignatureResultsAsync(reportSign),
                epoch, roundId);

            _logger.LogInformation(
                $"[CrossChain][Follower] Node:{nodeIndex} {messageId}-{epoch} Send signature to leader, Waiting for transmitted.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[CrossChain] Process partial signature request {messageId} failed.");
        }
    }
}