using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationPartSign : AsyncBackgroundJob<QueryReportSignatureRequest>, ITransientDependency
{
    private readonly IJobProvider _jobProvider;
    private readonly IPeerManager _peerManager;
    private readonly ISignatureProvider _signatureProvider;
    private readonly ILogger<GeneratePartialSignatureJob> _logger;
    private readonly IOracleContractProvider _oracleContractProvider;

    public AutomationPartSign(IPeerManager peerManager, ILogger<GeneratePartialSignatureJob> logger,
        IJobProvider jobProvider, IOracleContractProvider oracleContractProvider, ISignatureProvider signatureProvider)
    {
        _logger = logger;
        _jobProvider = jobProvider;
        _peerManager = peerManager;
        _signatureProvider = signatureProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(QueryReportSignatureRequest req)
    {
        var chainId = req.Context.ChainId;
        var reqId = req.Context.RequestId;
        var epoch = req.Context.Epoch;
        var roundId = req.Context.RoundId;

        try
        {
            var job = await _jobProvider.GetAsync(req.Context);
            if (job == null || job.State is RequestState.RequestCanceled) return;
            var commitment = await _oracleContractProvider.GetRequestCommitmentAsync(chainId, reqId);
            var originInput = RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData);

            if (req.CheckData != originInput.PerformData)
            {
                _logger.LogError("[Automation] Is different with leader check data.");
                return;
            }

            var partialSig = await _signatureProvider.GeneratePartialSignAsync(req.Context, originInput.PerformData);
            await _peerManager.CommitToLeaderAsync(p => p.CommitPartialSignatureAsync(new CommitPartialSignatureRequest
            {
                Context = req.Context,
                Signature = ByteString.CopyFrom(partialSig.Signature),
                Index = partialSig.Index
            }), epoch, roundId);

            _logger.LogInformation("[Automation] {reqId}-{epoch} Send signature to leader, Waiting for transmitted.",
                reqId, epoch);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Automation]{req} Sign report failed", reqId);
        }
    }
}