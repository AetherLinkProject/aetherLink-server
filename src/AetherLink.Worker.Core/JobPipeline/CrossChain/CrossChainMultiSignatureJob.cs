using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainKeyring;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainMultiSignatureJob : AsyncBackgroundJob<CrossChainMultiSignatureJobArgs>, ISingletonDependency
{
    private readonly object _finishLock = new();
    private readonly object _processLock = new();
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IStateProvider _stateProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<CrossChainMultiSignatureJob> _logger;
    private readonly Dictionary<long, IChainKeyring> _offChainKeyring;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public CrossChainMultiSignatureJob(ILogger<CrossChainMultiSignatureJob> logger, IObjectMapper objectMapper,
        IOptionsSnapshot<OracleInfoOptions> options, IBackgroundJobManager backgroundJobManager,
        ICrossChainRequestProvider crossChainRequestProvider, IEnumerable<IChainKeyring> offChainKeyring,
        IStateProvider stateProvider)
    {
        _logger = logger;
        _options = options.Value;
        _objectMapper = objectMapper;
        _stateProvider = stateProvider;
        _backgroundJobManager = backgroundJobManager;
        _crossChainRequestProvider = crossChainRequestProvider;
        _offChainKeyring = offChainKeyring.GroupBy(x => x.ChainId).Select(g => g.First())
            .ToDictionary(x => x.ChainId, y => y);
    }

    public override async Task ExecuteAsync(CrossChainMultiSignatureJobArgs args)
    {
        _logger.LogDebug($"[CrossChain] Get follower {args.Index} partial signature{JsonSerializer.Serialize(args)}");

        var reportContext = args.ReportContext;
        var messageId = reportContext.MessageId;
        var epoch = reportContext.Epoch;
        var roundId = reportContext.RoundId;
        var nodeIndex = args.Index;
        try
        {
            _logger.LogInformation($"[CrossChain][Leader] Get partial signature {messageId} {epoch} {nodeIndex}");
            var crossChainData = await _crossChainRequestProvider.GetAsync(messageId);
            if (crossChainData == null)
            {
                _logger.LogWarning($"[CrossChain][Leader] Ramp request {messageId} not exist.");
                return;
            }

            if (crossChainData.State == CrossChainState.RequestCanceled)
            {
                _logger.LogWarning($"[CrossChain][Leader] Ramp request {messageId} canceled");
                return;
            }

            var signatureId = IdGeneratorHelper.GenerateId(messageId, epoch, roundId);

            if (!string.IsNullOrEmpty(crossChainData.ResendTransactionId))
            {
                signatureId = IdGeneratorHelper.GenerateId(signatureId, crossChainData.ResendTransactionId);
            }

            if (_stateProvider.IsFinished(signatureId))
            {
                _logger.LogWarning($"[CrossChain][Leader] Ramp request {signatureId} is finished");
                return;
            }

            if (!_offChainKeyring.TryGetValue(reportContext.TargetChainId, out var signer))
            {
                _logger.LogWarning($"[CrossChain] Unknown target chain id: {reportContext.TargetChainId}");
                return;
            }

            // todo validate signature by chain client
            var report = new CrossChainReportDto
                { Message = crossChainData.Message, TokenTransferMetadata = crossChainData.TokenTransferMetadata };
            if (!signer.OffChainVerify(reportContext, nodeIndex, report, args.Signature))
            {
                _logger.LogWarning($"[CrossChain][Leader] Check {nodeIndex} Signature failed.");
                return;
            }

            var signatures = ProcessPartialSignature(signatureId, nodeIndex, args.Signature);

            if (!IsSignatureEnough(signatureId))
            {
                _logger.LogDebug($"[CrossChain][Leader] signature {signatureId} is not enough.");
                return;
            }

            if (!TryProcessFinishedFlag(signatureId))
            {
                _logger.LogDebug($"[CrossChain][Leader] {messageId} signature is finished.");
                return;
            }

            _logger.LogInformation($"[CrossChain][Leader] {signatureId} MultiSignature generate success.");

            var commitArgs = _objectMapper.Map<CrossChainMultiSignatureJobArgs, CrossChainCommitJobArgs>(args);
            commitArgs.PartialSignatures = signatures;
            commitArgs.CrossChainData = crossChainData;
            await _backgroundJobManager.EnqueueAsync(commitArgs);
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
            var currentDict = _stateProvider.GetCrossChainMultiSignature(signatureId) ?? new Dictionary<int, byte[]>();
            currentDict[nodeIndex] = signature;
            _stateProvider.SetCrossChainMultiSignature(signatureId, currentDict);
            return currentDict;
        }
    }

    private bool IsSignatureEnough(string signatureId)
    {
        lock (_processLock)
        {
            var count = _stateProvider.GetCrossChainMultiSignature(signatureId).Count;
            _logger.LogDebug($"[CrossChain][IsSignatureEnough] {signatureId} has {count} partial signature.");
            return count >= _options.PartialSignaturesThreshold;
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