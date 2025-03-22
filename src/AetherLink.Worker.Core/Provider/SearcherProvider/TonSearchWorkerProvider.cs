using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainHandler;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Exceptions;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider.TonIndexer;
using AetherLink.Worker.Core.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Encoders;
using TonSdk.Core;
using TonSdk.Core.Boc;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider.SearcherProvider;

public interface ITonSearchWorkerProvider
{
    public Task ExecuteSearchAsync();
}

public class TonSearchWorkerProvider : ITonSearchWorkerProvider, ISingletonDependency
{
    private readonly ISchedulerService _scheduler;
    private readonly IStorageProvider _storageProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly TonIndexerRouter _tonIndexerRouter;
    private readonly ITonStorageProvider _tonStorageProvider;
    private readonly ILogger<TonSearchWorkerProvider> _logger;
    private readonly IOptions<TonPublicOptions> _tonPublicOptions;
    private readonly ICrossChainRequestProvider _crossChainRequestProvider;

    public TonSearchWorkerProvider(ISchedulerService scheduler, ITonStorageProvider tonStorageProvider,
        TonIndexerRouter tonIndexerRouter, ILogger<TonSearchWorkerProvider> logger, IStorageProvider storageProvider,
        ICrossChainRequestProvider crossChainRequestProvider, IOptions<TonPublicOptions> tonPublicOptions,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _scheduler = scheduler;
        _tonIndexerRouter = tonIndexerRouter;
        _tonStorageProvider = tonStorageProvider;
        _storageProvider = storageProvider;
        _crossChainRequestProvider = crossChainRequestProvider;
        _tonPublicOptions = tonPublicOptions;
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteSearchAsync()
    {
        var indexerInfo = await _tonStorageProvider.GetTonIndexerInfoAsync();
        if (indexerInfo.LatestTransactionLt == "0" && _tonPublicOptions.Value.SkipTransactionLt != "0")
        {
            indexerInfo.LatestTransactionLt = _tonPublicOptions.Value.SkipTransactionLt;
        }

        var (transactionList, currentIndexerInfo) = await _tonIndexerRouter.GetSubsequentTransaction(indexerInfo);
        var dtNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        if (transactionList == null || transactionList.Count == 0)
        {
            if (currentIndexerInfo == null) return;
            currentIndexerInfo.IndexerTime = dtNow;
            await _tonStorageProvider.SetTonIndexerInfoAsync(currentIndexerInfo);
            return;
        }

        for (var i = 0; i < transactionList.Count; i++)
        {
            var tx = transactionList[i];
            if (!tx.Aborted && !tx.Bounced && tx.ExitCode == 0)
            {
                try
                {
                    switch (tx.OpCode)
                    {
                        case TonOpCodeConstants.ForwardTx:
                            await TonForwardTxHandle(tx);
                            break;
                        case TonOpCodeConstants.ReceiveTx:
                            await HandleTonReceiveTransaction(tx);
                            break;
                        case TonOpCodeConstants.ResendTx:
                            await HandleTonResendTx(tx);
                            break;
                        default:
                            continue;
                    }
                }
                catch (ProtocolException ex)
                {
                    _logger.LogWarning(
                        $"[TonIndexer] analysis ton transaction error:{ex.Message} , transaction hash:{tx.Hash}");
                }
            }
            else
            {
                _logger.LogInformation(
                    $"[Ton indexer] transaction execute error, detail message is:{JsonConvert.SerializeObject(tx)}");
            }

            // update indexer info
            indexerInfo.IndexerTime = dtNow;
            indexerInfo.SkipCount = 0;
            indexerInfo.LatestTransactionHash = tx.Hash;
            indexerInfo.LatestTransactionLt = tx.TransactionLt;
            indexerInfo.BlockHeight = tx.SeqNo;
            if (i < transactionList.Count - 1)
            {
                await _tonStorageProvider.SetTonIndexerInfoAsync(indexerInfo);
            }
            else
            {
                await _tonStorageProvider.SetTonIndexerInfoAsync(currentIndexerInfo);
            }
        }
    }

    private async Task TonForwardTxHandle(CrossChainToTonTransactionDto tx)
    {
        var forwardMessage = AnalysisForwardTransaction(tx);
        if (forwardMessage == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] AnalysisForwardTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
            return;
        }

        var rampMessageData = await _crossChainRequestProvider.GetAsync(forwardMessage.MessageId);
        if (rampMessageData == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] forward task, messageId:{forwardMessage.MessageId} not find in system, transaction hash:{tx.Hash}");
            return;
        }

        if (rampMessageData.ResendTransactionBlockHeight > tx.SeqNo)
        {
            _logger.LogWarning(
                $"[Ton indexer] forward message block height conflict, block:{rampMessageData.ResendTransactionBlockHeight}-{tx.SeqNo}, tx:{rampMessageData.ResendTransactionId}-{tx.Hash}");
            return;
        }

        if (rampMessageData.State != CrossChainState.Committed)
        {
            _logger.LogWarning(
                $"[Ton indexer] MessageId:{forwardMessage.MessageId} state error,current status is:{rampMessageData.State}, but receive a chain transaction");
        }

        // update message status
        rampMessageData.State = CrossChainState.Confirmed;
        rampMessageData.ResendTransactionBlockHeight = tx.SeqNo;
        rampMessageData.ResendTransactionId = tx.Hash;
        await _crossChainRequestProvider.SetAsync(rampMessageData);

        _logger.LogDebug($"[Ton indexer] {rampMessageData.ReportContext.MessageId} has been confirmed.");
        // cancel check transaction status task
        _scheduler.CancelScheduler(rampMessageData);
    }

    private async Task HandleTonResendTx(CrossChainToTonTransactionDto tx)
    {
        var resendMessage = AnalysisResendTransaction(tx);
        if (resendMessage == null)
        {
            _logger.LogInformation(
                $"[Ton indexer] AnalysisResendTransaction Get Null, CrossChainToTonTransactionDto is:{JsonConvert.SerializeObject(tx)}");
            return;
        }

        var crossChainData = await _crossChainRequestProvider.GetAsync(resendMessage.MessageId);
        if (crossChainData == null)
        {
            _logger.LogWarning(
                $"[Ton indexer] resend task, messageId:{resendMessage.MessageId} not find in system, block time:{tx.BlockTime} resend time:{resendMessage.ResendTime}");
            return;
        }

        if (crossChainData.ResendTransactionBlockHeight > tx.SeqNo)
        {
            _logger.LogWarning(
                $"[Ton Indexer] receive resend transaction, but block height conflict, messageId:{resendMessage.MessageId}, current block height:{crossChainData.ResendTransactionBlockHeight}-{tx.SeqNo}, hash compare:{crossChainData.ResendTransactionId}-{tx.Hash}");
            return;
        }

        crossChainData.ResendTransactionBlockTime =
            DateTimeOffset.FromUnixTimeSeconds(tx.BlockTime).DateTime;
        crossChainData.NextCommitDelayTime = (int)resendMessage.ResendTime;
        crossChainData.ResendTransactionId = tx.Hash;
        crossChainData.ResendTransactionBlockHeight = tx.SeqNo;
        _scheduler.StartScheduler(crossChainData, CrossChainSchedulerType.ResendPendingScheduler);

        _logger.LogInformation(
            $"[Ton indexer] received resend transaction messageId:{resendMessage.MessageId}, hash:{resendMessage.Hash}, block time:{tx.BlockTime}, resend time:{resendMessage.ResendTime}");
    }

    private async Task HandleTonReceiveTransaction(CrossChainToTonTransactionDto tx)
    {
        var receiveMessageDto = AnalysisReceiveTransaction(tx);
        var epochInfo = await _storageProvider.GetAsync<TonReceiveEpochInfoDto>(RedisKeyConstants.TonEpochStorageKey);
        if (epochInfo == null && receiveMessageDto.Epoch != 1)
        {
            _logger.LogWarning(
                $"[Ton indexer] received receive  transaction hash:{tx.Hash}, this message may be lost, receiveMessageDto is: {JsonConvert.SerializeObject(receiveMessageDto)}");
        }

        if (epochInfo != null && receiveMessageDto.Epoch < epochInfo.EpochId)
        {
            _logger.LogWarning(
                $"[Ton indexer] received receive  transaction hash:{tx.Hash}, this transaction has been dealed");
        }

        await _crossChainRequestProvider.StartCrossChainRequestFromTon(receiveMessageDto);
        epochInfo ??= new TonReceiveEpochInfoDto();
        epochInfo.EpochId = receiveMessageDto.Epoch;
        await _storageProvider.SetAsync(RedisKeyConstants.TonEpochStorageKey, epochInfo);

        _logger.LogInformation(
            $"[Ton indexer] received receive  transaction hash:{tx.Hash} has send to ");
    }

    private ResendMessageDto AnalysisResendTransaction(CrossChainToTonTransactionDto tonTransactionDto)
    {
        try
        {
            var bodySlice = Cell.From(tonTransactionDto.Body).Parse();
            var opCode = bodySlice.LoadInt(TonMetaDataConstants.OpCodeUintSize);
            if (opCode != TonOpCodeConstants.ResendTx)
            {
                _logger.LogError("[TonSearchWorkerProvider] Analysis Resend Transaction OpCode Error");
                return null;
            }

            var messageId = bodySlice.LoadBytes(TonMetaDataConstants.MessageIdBytesSize);
            var messageIdStr = Base64.ToBase64String(messageId);
            var result = new ResendMessageDto
            {
                MessageId = messageIdStr,
                TargetBlockHeight = tonTransactionDto.SeqNo,
                Hash = tonTransactionDto.Hash,
                TargetBlockGeneratorTime = tonTransactionDto.BlockTime,
            };

            if (bodySlice.LoadUInt(8) != TonResendTypeConstants.IntervalSeconds) return result;
            var intervalSeconds = (long)bodySlice.LoadUInt(TonMetaDataConstants.IntervalSecondsUIntSize);
            result.ResendTime = intervalSeconds;
            result.CheckCommitTime =
                intervalSeconds + tonTransactionDto.BlockTime + TonEnvConstants.ResendMaxWaitSeconds;

            return result;
        }
        catch (Exception ex)
        {
            throw new ProtocolException($"AnalysisResendTransaction Error ex:{ex}");
        }
    }

    private ForwardMessageDto AnalysisForwardTransaction(CrossChainToTonTransactionDto tonTransactionDto)
    {
        try
        {
            // MessageBody: OP, messageContext, ody
            var inMessageBodySlice = Cell.From(tonTransactionDto.Body).Parse();
            var opCode = inMessageBodySlice.LoadUInt(TonMetaDataConstants.OpCodeUintSize);
            var messageContext = inMessageBodySlice.LoadRef().Parse();
            var messageId = messageContext.LoadBytes(TonMetaDataConstants.MessageIdBytesSize);

            return new() { MessageId = Base64.ToBase64String(messageId), };
        }
        catch (Exception e)
        {
            throw new ProtocolException($"AnalysisForwardTransaction error,err:{e.Message}");
        }
    }

    private ReceiveMessageDto AnalysisReceiveTransaction(CrossChainToTonTransactionDto tonTransactionDto)
    {
        try
        {
            var receiveSlice = Cell.From(tonTransactionDto.OutMessage).Parse();
            var epochId = (long)receiveSlice.LoadInt(TonMetaDataConstants.EpochIntSize);
            var targetChainId = (long)receiveSlice.LoadInt(TonMetaDataConstants.ChainIdIntSize);
            var targetChainProvider = _serviceProvider.GetServices<IChainReader>()
                .FirstOrDefault(f => f.ChainId == targetChainId);
            if (targetChainProvider == null)
            {
                throw new Exception($"[TonIndexer] IChainReader target chainId:{targetChainId} not support");
            }

            var targetContractAddress =
                targetChainProvider.ConvertBytesToAddressStr(receiveSlice.LoadRef().Parse().Bits.ToBytes());
            var senderTonContractAddress = receiveSlice.LoadAddress();
            var sender = senderTonContractAddress.ToString(AddressType.Base64,
                new AddressStringifyOptions(senderTonContractAddress.IsBounceable(),
                    senderTonContractAddress.IsTestOnly(), false));
            var message = Base64.ToBase64String(TonHelper.ConvertMessageCellToBytes(receiveSlice.LoadRef()));

            TokenTransferMetadataDto tokenTransferMetadataDto = null;
            if (receiveSlice.Refs.Length <= 0)
                return new()
                {
                    MessageId = tonTransactionDto.Hash,
                    Sender = sender,
                    Epoch = epochId,
                    SourceChainId = ChainIdConstants.TON,
                    TargetChainId = targetChainId,
                    TargetContractAddress = targetContractAddress,
                    TransactionTime = tonTransactionDto.BlockTime * 1000,
                    Message = message,
                    TokenTransferMetadataDtoInfo = tokenTransferMetadataDto,
                };

            var extraDataRefCell = receiveSlice.LoadRef().Parse();
            var tokenTargetChainId = (long)extraDataRefCell.LoadInt(TonMetaDataConstants.ChainIdIntSize);
            var contractAddress =
                targetChainProvider.ConvertBytesToAddressStr(extraDataRefCell.LoadRef().Parse().Bits.ToBytes());
            var tokenAddress = extraDataRefCell.LoadRef().Parse().LoadAddress();
            var tokenAddressStr = tokenAddress.ToString(AddressType.Base64,
                new AddressStringifyOptions(tokenAddress.IsBounceable(), tokenAddress.IsTestOnly(), false));
            var amount = (long)extraDataRefCell.LoadUInt(TonMetaDataConstants.AmountUIntSize);
            tokenTransferMetadataDto = new TokenTransferMetadataDto
            {
                TargetChainId = tokenTargetChainId,
                TokenAddress = tokenAddressStr,
                Amount = amount
            };

            return new()
            {
                MessageId = tonTransactionDto.Hash,
                Sender = sender,
                Epoch = epochId,
                SourceChainId = ChainIdConstants.TON,
                TargetChainId = targetChainId,
                TargetContractAddress = targetContractAddress,
                TransactionTime = tonTransactionDto.BlockTime * 1000,
                Message = message,
                TokenTransferMetadataDtoInfo = tokenTransferMetadataDto,
            };
        }
        catch (Exception e)
        {
            throw new ProtocolException($"AnalysisReceiveTransaction Error error message:{e.Message} ");
        }
    }
}