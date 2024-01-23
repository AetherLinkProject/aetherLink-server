using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AetherLink.Contracts.Oracle;
using AetherLink.Contracts.VRF.Coordinator;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class VRFProcessJob : AsyncBackgroundJob<VRFJobArgs>, ITransientDependency
{
    private readonly OracleInfoOptions _options;
    private readonly IRetryProvider _retryProvider;
    private readonly ILogger<VRFProcessJob> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly IOracleContractProvider _oracleContractProvider;

    public VRFProcessJob(ILogger<VRFProcessJob> logger, IOptionsSnapshot<OracleInfoOptions> options,
        IContractProvider contractProvider, IOracleContractProvider oracleContractProvider,
        IRetryProvider retryProvider)
    {
        _logger = logger;
        _options = options.Value;
        _retryProvider = retryProvider;
        _contractProvider = contractProvider;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(VRFJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;

        if (!_options.ChainConfig.TryGetValue(chainId, out var vrfInfo))
        {
            _logger.LogWarning("[VRF] Unsupported chain{chainId}", chainId);
            return;
        }

        try
        {
            _logger.LogInformation("[VRF] Start Generate vrf proof reqId {reqId}.", reqId);
            var vrfKp = CryptoHelper.FromPrivateKey(ByteArrayHelper.HexStringToByteArray(vrfInfo.VRFSecret));

            // check vrf request transaction exist.
            var commitment =
                await _oracleContractProvider.GetRequestCommitmentAsync(args.ChainId, args.TransactionId, reqId);
            var specificData = SpecificData.Parser.ParseFrom(commitment.SpecificData);

            // validate keyHash
            if (HashHelper.ComputeFrom(vrfKp.PublicKey.ToHex()) != specificData.KeyHash)
            {
                _logger.LogInformation("[VRF] This vrf job isn't belong to this node. reqId {reqId}", reqId);
                return;
            }

            // get random hash in ConsensusContract by blockHeight
            var random = await _contractProvider.GetRandomHashAsync(specificData.BlockNumber, chainId);
            var alpha = HashHelper.ConcatAndCompute(random, specificData.PreSeed).ToByteArray();
            var proof = CryptoHelper.ECVrfProve(vrfKp, alpha);

            // Verify that proof, return false if failed.
            if (!VerifyProof(vrfKp.PublicKey, alpha, proof))
            {
                _logger.LogError("[VRF] Verify that proof failed. reqId {reqId}", reqId);
                return;
            }

            _logger.LogInformation("[VRF] Verify proof success, ready to send transmit.");

            // generate vrf prove in report
            var transmitInput = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, reqId,
                args.TransactionId, await _oracleContractProvider.GetOracleLatestEpochAndRoundAsync(chainId),
                ByteString.CopyFrom(proof));

            var signatures = new List<ByteString> { GenerateSignature(vrfKp.PrivateKey, transmitInput) };
            transmitInput.Signatures.AddRange(signatures);

            // send to oracle contract
            var transactionId = await _contractProvider.SendTransmitAsync(chainId, transmitInput);
            _logger.LogInformation("[VRF] Transmit transaction {transactionId}, reqId {reqId}", transactionId, reqId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[VRF] ReqId {reqId} generate VRF Failed", reqId);
            await _retryProvider.RetryAsync(args);
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
}