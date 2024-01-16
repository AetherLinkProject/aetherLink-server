using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AetherLink.Contracts.Oracle;
using AetherLink.Contracts.VRF.Coordinator;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class VRFProcessJob : AsyncBackgroundJob<VRFJobArgs>, ISingletonDependency
{
    private readonly IPeerManager _peerManager;
    private readonly OracleInfoOptions _options;
    private readonly ILogger<VRFProcessJob> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;
    private readonly ConcurrentDictionary<string, int> _retryCount = new();

    public VRFProcessJob(ILogger<VRFProcessJob> logger, IOptionsSnapshot<OracleInfoOptions> options,
        IBackgroundJobManager backgroundJobManager, IContractProvider contractProvider, IPeerManager peerManager,
        IOracleContractProvider oracleContractProvider, IOptionsSnapshot<ProcessJobOptions> processJobOptions)
    {
        _logger = logger;
        _options = options.Value;
        _peerManager = peerManager;
        _contractProvider = contractProvider;
        _processJobOptions = processJobOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(VRFJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;

        if (!_options.ChainConfig.TryGetValue(chainId, out var vrfInfo)) return;

        try
        {
            _logger.LogInformation("[VRF] Start Generate vrf proof reqId {reqId}.", reqId);
            var vrfKp = CryptoHelper.FromPrivateKey(ByteArrayHelper.HexStringToByteArray(vrfInfo.VRFSecret));

            // check vrf request transaction exist.
            var commitment =
                await _oracleContractProvider.GetCommitmentAsync(args.ChainId, args.TransactionId, reqId);
            var specificData = SpecificData.Parser.ParseFrom(commitment.SpecificData);

            // validate keyHash
            if (HashHelper.ComputeFrom(vrfKp.PublicKey.ToHex()) != specificData.KeyHash)
            {
                _logger.LogWarning("[VRF] This vrf job isn't belong to this node. reqId {reqId}", reqId);
                return;
            }

            // get random hash in ConsensusContract by blockHeight
            var random = await _contractProvider.GetRandomHashAsync(specificData.BlockNumber, chainId);
            var alpha = HashHelper.ConcatAndCompute(random, specificData.PreSeed).ToByteArray();

            // todo For test, will remove
            // var alpha = HashHelper.ConcatAndCompute(random, Hash.Empty).ToByteArray();
            var proof = CryptoHelper.ECVrfProve(vrfKp, alpha);

            // Verify that proof, return false if failed.
            if (!VerifyProof(vrfKp.PublicKey, alpha, proof))
            {
                _logger.LogError("[VRF] Verify that proof failed. reqId {reqId}", reqId);
                return;
            }

            _logger.LogInformation("[VRF] Verify proof success, ready to send transmit.");

            // generate vrf prove in report
            var transmitInput = new TransmitInput
            {
                Report = new Report
                {
                    Result = ByteString.CopyFrom(proof),
                    OnChainMetadata = commitment.ToByteString(),
                    Error = ByteString.Empty,
                    OffChainMetadata = ByteString.Empty
                }.ToByteString()
            };

            // get config fill ReportContext
            transmitInput.ReportContext.Add(await _peerManager.GetLatestConfigDigestAsync(chainId));

            // get latest round fill ReportContext
            transmitInput.ReportContext.Add(HashHelper.ComputeFrom(await _peerManager.GetEpochAsync(chainId)));
            transmitInput.ReportContext.Add(HashHelper.ComputeFrom(0));

            var signatures = new List<ByteString>();
            signatures.Add(GenerateSignature(vrfKp.PrivateKey, transmitInput));
            transmitInput.Signatures.AddRange(signatures);

            // send to oracle contract
            var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitInput);
            _logger.LogInformation("[VRF] Transmit transaction {transactionId}, reqId {reqId}", transactionId, reqId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[VRF] ReqId {reqId} generate VRF Failed", reqId);
            await RetryAsync(args);
        }
    }

    private ByteString GenerateSignature(byte[] privateKey, TransmitInput input)
    {
        var hash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Report.ToByteArray()),
            HashHelper.ComputeFrom(input.ReportContext.ToString()));
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, hash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private bool VerifyProof(byte[] pubKey, byte[] alpha, byte[] proof)
    {
        try
        {
            CryptoHelper.ECVrfVerify(pubKey, alpha, proof);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[VRF] ECVrfVerify Failed!");
            return false;
        }
    }

    private async Task RetryAsync(VRFJobArgs args)
    {
        _logger.LogInformation("[VRF] Retry process, reqId:{Req}, chainId:{ChainId}", args.RequestId, args.ChainId);
        var id = IdGeneratorHelper.GenerateId(args.RequestId, args.RoundId);
        _retryCount.TryGetValue(id, out var time);
        if (time < _processJobOptions.RetryCount)
        {
            _retryCount[id] = time + 1;
            await _backgroundJobManager.EnqueueAsync(args, delay: TimeSpan.FromSeconds(time));
        }
    }
}