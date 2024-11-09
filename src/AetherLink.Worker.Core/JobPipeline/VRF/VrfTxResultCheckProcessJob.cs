using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Common.ContractHandler;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline.VRF;

public class VrfTxResultCheckProcessJob : AsyncBackgroundJob<VrfTxResultCheckJobArgs>, ITransientDependency
{
    private readonly IVrfProvider _vrfProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ProcessJobOptions _jobOptions;
    private readonly IRetryProvider _retryProvider;
    private readonly IBackgroundJobManager _jobManager;
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<VrfTxResultCheckProcessJob> _logger;


    public VrfTxResultCheckProcessJob(ILogger<VrfTxResultCheckProcessJob> logger, IContractProvider contractProvider,
        IVrfProvider vrfProvider, IRetryProvider retryProvider, IOptionsSnapshot<ProcessJobOptions> processJobOptions,
        IBackgroundJobManager jobManager, IObjectMapper objectMapper)
    {
        _logger = logger;
        _jobManager = jobManager;
        _vrfProvider = vrfProvider;
        _objectMapper = objectMapper;
        _retryProvider = retryProvider;
        _contractProvider = contractProvider;
        _jobOptions = processJobOptions.Value;
    }

    public override async Task ExecuteAsync(VrfTxResultCheckJobArgs args)
    {
        var chainId = args.ChainId;
        var transactionId = args.TransmitTransactionId;
        var requestId = args.RequestId;
        _logger.LogInformation("[VRF] Start to check vrf requestId:{reqId} transmit transaction {txId} result, ",
            requestId, transactionId);

        var vrfJob = await _vrfProvider.GetAsync(chainId, requestId);
        if (vrfJob == null)
        {
            _logger.LogError("[VRF] VRF chainId {chain} job {reqId} not exist", chainId, requestId);
            return;
        }

        var txResult = await _contractProvider.GetTxResultAsync(chainId, transactionId);
        switch (txResult.Status)
        {
            case TransactionState.Mined:
                if (requestId != _contractProvider.ParseTransmitted(txResult).RequestId.ToHex())
                {
                    _logger.LogError("[VRF] Job {ReqId} transactionId {txId} not match.", requestId, transactionId);
                    return;
                }

                vrfJob.Status = VrfJobState.Consumed;
                await _vrfProvider.SetAsync(vrfJob);

                _logger.LogInformation("[VRF] {ReqId} Transmitted validate successful.", requestId);

                break;
            case TransactionState.Pending:
                _logger.LogInformation("[VRF] Request {ReqId} transaction {txId} is pending, will retry later.",
                    requestId, transactionId);

                await _retryProvider.RetryWithIdAsync(args, GenerateVrfRetryId(chainId, requestId, transactionId),
                    untilFailed: true, delayDelta: _jobOptions.TransactionResultDelay);
                break;
            case TransactionState.NotExisted:
                _logger.LogInformation("[VRF] Job {ReqId} transactionId {txId} is not exist, will retry later.",
                    requestId, transactionId);

                await _retryProvider.RetryWithIdAsync(args, GenerateVrfRetryId(chainId, requestId, transactionId),
                    backOff: true);
                break;
            default:
                _logger.LogWarning("[VRF] Request {ReqId} is {state} not Mined, transactionId {txId} error: {error}",
                    requestId, txResult.Status, transactionId, txResult.Error);

                // for canceled request 
                if (!string.IsNullOrEmpty(txResult.Error) && txResult.Error.Contains("Transaction expired"))
                {
                    if (!await _contractProvider.IsTransactionConfirmed(chainId, args.BlockHeight, args.BlockHash))
                    {
                        _logger.LogWarning(
                            "This transaction {txId} has not been confirmed in blockHeight {height} with blockHash {hash}",
                            transactionId, args.BlockHeight, args.BlockHash);
                    }

                    _logger.LogWarning("[VRF] Job {ReqId} is expired, will retry with chain height.", requestId);
                    await _jobManager.EnqueueAsync(_objectMapper.Map<VrfTxResultCheckJobArgs, VRFJobArgs>(args));
                }

                break;
        }
    }

    private string GenerateVrfRetryId(string chainId, string requestId, string transactionId) =>
        IdGeneratorHelper.GenerateId("vrf", "transaction", chainId, requestId, transactionId);
}