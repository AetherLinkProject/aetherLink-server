using System;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestMultiSignatureJob : AsyncBackgroundJob<RampRequestMultiSignatureJobArgs>, ITransientDependency
{
    private readonly object _finishLock = new();
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IRampMessageProvider _messageProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<RampRequestMultiSignatureJob> _logger;

    public RampRequestMultiSignatureJob(ILogger<RampRequestMultiSignatureJob> logger, IStateProvider stateProvider,
        IRampMessageProvider messageProvider, IPeerManager peerManager, IBackgroundJobManager backgroundJobManager,
        IObjectMapper objectMapper)
    {
        _logger = logger;
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
            var messageData = await _messageProvider.GetAsync(chainId, messageId);
            if (messageData == null) return;
            
            var signatureId = IdGeneratorHelper.GenerateMultiSignatureId(chainId, messageId, epoch, roundId);
            if (_stateProvider.IsFinished(signatureId)) return;

            // todo insert follower's signature


            if (!TryProcessFinishedFlag(signatureId))
            {
                _logger.LogDebug($"[Ramp][Leader] {messageId} signature is finished.");
                return;
            }

            _logger.LogInformation($"[Ramp][Leader] {messageId} MultiSignature generate success.");

            // send transaction
            var commitTransactionId = "test-commitTransactionId";
            // broadcast transaction to follower

            var finishArgs = _objectMapper.Map<RampRequestMultiSignatureJobArgs, RampRequestCommitResultJobArgs>(args);
            finishArgs.TransactionId = commitTransactionId;
            await _backgroundJobManager.EnqueueAsync(finishArgs);

            var txResult = _objectMapper.Map<RampRequestMultiSignatureJobArgs, RampCommitResultRequest>(args);
            txResult.CommitTransactionId = commitTransactionId;
            await _peerManager.BroadcastAsync(p => p.RampCommitResultAsync(txResult));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
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
}