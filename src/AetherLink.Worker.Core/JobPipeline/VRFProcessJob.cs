using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AetherLink.Contracts.Oracle;
using AetherLink.Contracts.VRF.Coordinator;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Reporter;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline;

public class VRFProcessJob : AsyncBackgroundJob<VRFJobArgs>, ITransientDependency
{
    private readonly IVRFReporter _vrfReporter;
    private readonly IVrfProvider _vrfProvider;
    private readonly OracleInfoOptions _options;
    private readonly IObjectMapper _objectMapper;
    private readonly IRetryProvider _retryProvider;
    private readonly ILogger<VRFProcessJob> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public VRFProcessJob(ILogger<VRFProcessJob> logger, IOptionsSnapshot<OracleInfoOptions> options,
        IContractProvider contractProvider, IOracleContractProvider oracleContractProvider, IVrfProvider vrfProvider,
        IRetryProvider retryProvider, IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        IOptionsSnapshot<ProcessJobOptions> processJobOptions, IVRFReporter vrfReporter)
    {
        _logger = logger;
        _options = options.Value;
        _vrfProvider = vrfProvider;
        _vrfReporter = vrfReporter;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _contractProvider = contractProvider;
        _processJobOptions = processJobOptions.Value;
        _backgroundJobManager = backgroundJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(VRFJobArgs args)
    {
        var chainId = args.ChainId;
        var reqId = args.RequestId;
        if (args.StartTime == 0) args.StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

        if (!_options.ChainConfig.TryGetValue(chainId, out var vrfInfo))
        {
            _logger.LogWarning("[VRF] Unsupported chain{chainId}", chainId);
            return;
        }

        if (await CheckVrfJobConsumed(chainId, reqId))
        {
            _logger.LogWarning("[VRF] {reqId} is a task that has already been consumed", reqId);
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

            // generate vrf prove in report
            var transmitInput = await _oracleContractProvider.GenerateTransmitDataAsync(chainId, reqId,
                args.TransactionId, await _oracleContractProvider.GetOracleLatestEpochAndRoundAsync(chainId),
                ByteString.CopyFrom(await GenerateVrf(chainId, vrfKp, specificData)));
            transmitInput.Signatures.AddRange(new List<ByteString>
                { GenerateSignature(vrfKp.PrivateKey, transmitInput) });

            _logger.LogInformation("[VRF] Generate proof success, ready to send transmit.");

            // send to oracle contract
            var refBlockHeight = args.BlockHeight;
            var regBlockHash = args.BlockHash;
            _logger.LogDebug("[VRF] Request {id} reference blockHeight: {height} blockHash: {hash}", reqId,
                refBlockHeight, regBlockHash);

            var transactionId = await _contractProvider.SendTransmitWithRefHashAsync(chainId, transmitInput,
                refBlockHeight, regBlockHash);

            _vrfReporter.RecordVrfExecuteTime(chainId, reqId,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - args.StartTime);

            _logger.LogInformation("[VRF] reqId {reqId} Transmit transaction: {txId}, ready to check result", reqId,
                transactionId);

            var vrfTransmitResult = _objectMapper.Map<VRFJobArgs, VrfTxResultCheckJobArgs>(args);
            vrfTransmitResult.TransmitTransactionId = transactionId;
            var vrfJob = _objectMapper.Map<VRFJobArgs, VrfJobDto>(vrfTransmitResult);
            vrfJob.Status = VrfJobState.CheckPending;

            await _vrfProvider.SetAsync(vrfJob);
            await _backgroundJobManager.EnqueueAsync(vrfTransmitResult,
                delay: TimeSpan.FromSeconds(_processJobOptions.TransactionResultDelay));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[VRF] ReqId {reqId} generate VRF Failed", reqId);
            await _retryProvider.RetryWithIdAsync(args, GenerateVrfRetryId(chainId, reqId));
        }
    }

    private async Task<bool> CheckVrfJobConsumed(string chainId, string requestId)
        => await _vrfProvider.GetAsync(chainId, requestId) is { Status: VrfJobState.Consumed };

    private async Task<byte[]> GenerateVrf(string chainId, ECKeyPair vrfKp, SpecificData specificData)
        => CryptoHelper.ECVrfProve(vrfKp,
            HashHelper.ConcatAndCompute(await _contractProvider.GetRandomHashAsync(specificData.BlockNumber, chainId),
                specificData.PreSeed).ToByteArray());

    private ByteString GenerateSignature(byte[] privateKey, TransmitInput input)
    {
        var hash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Report.ToByteArray()),
            HashHelper.ComputeFrom(input.ReportContext.ToString()));
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, hash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private string GenerateVrfRetryId(string chainId, string requestId) =>
        IdGeneratorHelper.GenerateId("vrf", "execute", chainId, requestId);
}