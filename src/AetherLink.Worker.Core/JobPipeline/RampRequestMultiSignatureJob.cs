using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestMultiSignatureJob : AsyncBackgroundJob<RampRequestMultiSignatureJobArgs>, ITransientDependency
{
    private readonly object _finishLock = new();
    private readonly object _processLock = new();
    private readonly TonHelper _tonHelper;
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IRampMessageProvider _messageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<RampRequestMultiSignatureJob> _logger;

    public RampRequestMultiSignatureJob(ILogger<RampRequestMultiSignatureJob> logger, IStateProvider stateProvider,
        IRampMessageProvider messageProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager,
        IObjectMapper objectMapper, TonHelper tonHelper, IOptionsSnapshot<OracleInfoOptions> options)
    {
        _logger = logger;
        _tonHelper = tonHelper;
        _options = options.Value;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _messageProvider = messageProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(RampRequestMultiSignatureJobArgs args)
    {
        var chainId = args.ChainId;
        var messageId = args.MessageId;
        var epoch = args.Epoch;
        var nodeIndex = args.Index;
        var roundId = args.RoundId;
        try
        {
            _logger.LogInformation($"Get partial signature {messageId} {epoch} {nodeIndex}");
            var messageData = await _messageProvider.GetAsync(messageId);
            if (messageData == null) return;

            var signatureId = IdGeneratorHelper.GenerateId(messageId, epoch, roundId);
            if (_stateProvider.IsFinished(signatureId)) return;

            var metadata = new CrossChainForwardMessageDto
            {
                MessageId = messageData.MessageId,
                SourceChainId = messageData.SourceChainId,
                TargetChainId = messageData.TargetChainId,
                Sender = messageData.Sender,
                Receiver = messageData.Receiver,
                Message = messageData.Data
            };
            var checkResult = _tonHelper.CheckSign(metadata, args.Signature, nodeIndex);

            if (!checkResult)
            {
                _logger.LogWarning(
                    $"[Ramp] check {nodeIndex} signature failed, please check signature and data {messageData.Data}.");
                return;
            }

            _logger.LogDebug($"[Ramp][Leader] signature checked result {checkResult}.");

            var signatures = ProcessPartialSignature(signatureId, nodeIndex, args.Signature);

            if (!IsSignatureEnough(signatureId))
            {
                _logger.LogDebug($"[Ramp][Leader] signature {signatureId} is not enough.");
                return;
            }

            if (!TryProcessFinishedFlag(signatureId))
            {
                _logger.LogDebug($"[Ramp][Leader] {messageId} signature is finished.");
                return;
            }

            _logger.LogInformation($"[Ramp][Leader] {messageId} MultiSignature generate success.");

            var commitTransactionId = await SendTransactionAsync(metadata, signatures);

            if (string.IsNullOrEmpty(commitTransactionId)) return;

            _logger.LogInformation(
                $"[Ramp][Leader] {messageId} send transaction success, transaction id: {commitTransactionId}.");

            var finishArgs = _objectMapper.Map<RampRequestMultiSignatureJobArgs, RampRequestCommitResultJobArgs>(args);
            finishArgs.TransactionId = commitTransactionId;
            await _backgroundJobManager.EnqueueAsync(finishArgs);

            var txResult = _objectMapper.Map<RampRequestMultiSignatureJobArgs, RampCommitResultRequest>(args);
            txResult.CommitTransactionId = commitTransactionId;
            await _peerManager.BroadcastAsync(p => p.RampCommitResultAsync(txResult));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Ramp] multi signature fail.");
        }
    }

    private Dictionary<int, byte[]> ProcessPartialSignature(string signatureId, int nodeIndex, byte[] signature)
    {
        lock (_processLock)
        {
            var currentDict = _stateProvider.GetTonMultiSignature(signatureId) ?? new Dictionary<int, byte[]>();

            currentDict[nodeIndex] = signature;
            _stateProvider.SetTonMultiSignature(signatureId, currentDict);
            return currentDict;
        }
    }

    private bool IsSignatureEnough(string signatureId)
    {
        lock (_processLock)
        {
            var count = _stateProvider.GetTonMultiSignature(signatureId).Count;
            _logger.LogDebug($"[Ramp][IsSignatureEnough] {signatureId} has {count} partial signature.");
            return count > _options.PartialSignaturesThreshold;
        }
    }


    private bool TryProcessFinishedFlag(string signatureId)
    {
        lock (_finishLock)
        {
            if (_stateProvider.IsFinished(signatureId)) return false;
            _stateProvider.SetFinishedFlag(signatureId);
            return true;
        }
    }

    private async Task<string> SendTransactionAsync(CrossChainForwardMessageDto metadata,
        Dictionary<int, byte[]> signatures)
    {
        for (var i = 0; i < RetryConstants.DefaultDelay; i++)
        {
            var commitTransactionId = await _tonHelper.SendTransaction(metadata, signatures);
            if (!string.IsNullOrEmpty(commitTransactionId)) return commitTransactionId;

            _logger.LogError(
                $"[Ramp][Leader] {metadata.MessageId} send transaction failed in {i} times, will send it later.");
            Thread.Sleep((i + 1) * 1000 * 2);
        }

        // If we get here, it means we have exhausted the retry count
        // So we execute the operation one last time, without any retries
        return await _tonHelper.SendTransaction(metadata, signatures);
    }
}