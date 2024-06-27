using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationTransmit : AsyncBackgroundJob<CommitPartialSignatureRequest>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IJobProvider _jobProvider;
    private readonly IStateProvider _stateProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<AutomationTransmit> _logger;
    private readonly ISignatureProvider _signatureProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationTransmit(ILogger<AutomationTransmit> logger, IStateProvider stateProvider,
        IContractProvider contractProvider, IOracleContractProvider oracleContractProvider, IJobProvider jobProvider,
        IPeerManager peerManager, IBackgroundJobManager backgroundJobManager, ISignatureProvider signatureProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _stateProvider = stateProvider;
        _contractProvider = contractProvider;
        _signatureProvider = signatureProvider;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(CommitPartialSignatureRequest req)
    {
        var reqId = req.Context.RequestId;
        var chainId = req.Context.ChainId;
        var roundId = req.Context.RoundId;
        var epoch = req.Context.Epoch;

        try
        {
            var job = await _jobProvider.GetAsync(req.Context);
            if (job == null || job.State == RequestState.RequestCanceled) return;

            var multiSignId = IdGeneratorHelper.GenerateMultiSignatureId(chainId, reqId, epoch, roundId);
            if (_stateProvider.IsFinished(multiSignId)) return;

            if (!_signatureProvider.ProcessMultiSignAsync(req.Context, req.Index, req.Signature.ToByteArray()))
            {
                _logger.LogError($"[Automation] {reqId}-{epoch} process {req.Index} failed.");
                return;
            }

            if (!_stateProvider.GetMultiSignature(multiSignId).IsEnoughPartialSig())
            {
                _logger.LogDebug($"[Automation] {reqId}-{epoch} is not enough, no need to generate signature.");
                return;
            }

            if (_stateProvider.IsFinished(multiSignId))
            {
                _logger.LogDebug($"[Automation] {reqId}-{epoch} signature is finished.");
                return;
            }

            _stateProvider.SetFinishedFlag(multiSignId);
            _logger.LogInformation($"[Automation] {reqId}-{epoch} MultiSignature pre generate success.");

            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, reqId);
            var originInput = RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData);
            var transmitData =
                await _oracleContractProvider.GenerateTransmitDataAsync(chainId, reqId, epoch, originInput.PerformData);
            var multiSignature = _stateProvider.GetMultiSignature(multiSignId);
            multiSignature.TryGetSignatures(out var signature);
            transmitData.Signatures.AddRange(signature);

            var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitData);
            _logger.LogInformation($"[Automation] {reqId}-{epoch} Transmit transaction {transactionId}");

            await _backgroundJobManager.EnqueueAsync(new TransmitResultProcessJobArgs
            {
                ChainId = chainId,
                RequestId = reqId,
                Epoch = epoch,
                TransactionId = transactionId,
                RoundId = roundId,
            });

            await _peerManager.BroadcastAsync(p => p.BroadcastTransmitResultAsync(new()
            {
                Context = req.Context,
                TransactionId = transactionId
            }));
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[Automation] {reqId}-{epoch} send transaction Failed.");
        }
    }
}