using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public sealed class TonCenterApi:ITonIndexer,ISingletonDependency
{
    private readonly TonCenterApiConfig _apiConfig;
    private readonly TonHelper _tonHelper;
    private readonly IHttpClientFactory _clientFactory;
    private readonly TonCenterRequestLimit _requestLimit;
    
    public TonCenterApi(IOptionsSnapshot<IConfiguration> snapshotConfig, TonHelper tonHelper, IHttpClientFactory  clientFactory)
    {
         _apiConfig = snapshotConfig.Value.GetSection("Chains:ChainInfos:Ton:Indexer:TonCenter").Get<TonCenterApiConfig>();
         _tonHelper = tonHelper;
         _clientFactory = clientFactory;

         var limitCount = string.IsNullOrEmpty(_apiConfig.ApiKey)
             ? _apiConfig.NoApiKeyPerSecondRequestLimit
             : _apiConfig.ApiKeyPerSecondRequestLimit;

         _requestLimit = new TonCenterRequestLimit(limitCount);
    }

    public int ApiWeight => _apiConfig.Weight;

    [ItemCanBeNull]
    public async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        var url = $"{_apiConfig.Url}/transactions?hash={txId}";
        var transactions = await GetDeserializeRequest<TransactionsResponse>(url);

        if (transactions.Transactions.Count == 0)
        {
            return null;
        }

        var originalTx = transactions.Transactions[0];
        var tx = new CrossChainToTonTransactionDto
        {
            OpCode = originalTx.InMsg.Opcode,
            WorkChain = originalTx.BlockRef.Workchain,
            Shard = originalTx.BlockRef.Shard,
            SeqNo = originalTx.BlockRef.Seqno,
            TraceId = originalTx.TraceId,
            Hash = originalTx.Hash,
            Aborted = originalTx.Description.Aborted,
            PrevHash = originalTx.PrevTransHash,
            BlockTime = originalTx.Now,
            Body = originalTx.InMsg.MessageContent.Body,
            Success = originalTx.Description.ComputePh.Success && originalTx.Description.Action.Success,
            ExitCode = originalTx.Description.ComputePh.ExitCode,
            Bounce = originalTx.InMsg.Bounce,
            Bounced = originalTx.InMsg.Bounced
        };

        return tx;
    }

    public async Task<TransactionAnalysisDto<CrossChainToTonTransactionDto, TonIndexerDto>> GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
    {
        var url = $"{_apiConfig.Url}/transactions?account={_tonHelper.TonOracleContractAddress}&start_utime={tonIndexerDto.LatestTransactionTime}&limit=20&offset=0&sort=asc";
        var transactionResp = await GetDeserializeRequest<TransactionsResponse>(url);
        
        var preHash = tonIndexerDto.LatestTransactionHash;
        Transaction preTx = null;

        var resendTxList = new List<CrossChainToTonTransactionDto>();
        var forwardTxList = new List<CrossChainToTonTransactionDto>();
        var receiveTxList = new List<CrossChainToTonTransactionDto>();
        
        // transactions has been order by asc 
        foreach (var originalTx in transactionResp.Transactions)
        {
            if(originalTx.PrevTransHash == preHash)
            {
                preHash = originalTx.Hash;
                preTx = originalTx;
                
                var tx = new CrossChainToTonTransactionDto();
                switch (originalTx.InMsg.Opcode)
                {
                    case TonOpCodeConstants.ForwardTx:
                        tx.OpCode = TonOpCodeConstants.ForwardTx;
                        forwardTxList.Add(tx);
                      break;
                    case TonOpCodeConstants.ResendTx:
                        tx.OpCode = TonOpCodeConstants.ResendTx;
                        resendTxList.Add(tx);
                        break;
                    case TonOpCodeConstants.ReceiveTx:
                        tx.OpCode = TonOpCodeConstants.ReceiveTx;
                        receiveTxList.Add(tx);
                        break;
                    default:
                        continue;
                }

                tx.WorkChain = originalTx.BlockRef.Workchain;
                tx.Shard = originalTx.BlockRef.Shard;
                tx.SeqNo = originalTx.BlockRef.Seqno;
                tx.TraceId = originalTx.TraceId;
                tx.Hash = originalTx.Hash;
                tx.Aborted = originalTx.Description.Aborted;
                tx.PrevHash = originalTx.PrevTransHash;
                tx.BlockTime = originalTx.Now;
                tx.Body = originalTx.InMsg.MessageContent.Body;
                tx.Success = originalTx.Description.ComputePh.Success && originalTx.Description.Action.Success;
                tx.ExitCode = originalTx.Description.ComputePh.ExitCode;
                tx.Bounce = originalTx.InMsg.Bounce;
                tx.Bounced = originalTx.InMsg.Bounced;
            }
        }

        return new TransactionAnalysisDto<CrossChainToTonTransactionDto, TonIndexerDto>
        {
            ResendTxList = resendTxList,
            ForwardTxList = forwardTxList,
            ReceiveTxList = receiveTxList,
            LatestTransactions = preTx == null ? null : new TonIndexerDto(){BlockHeight =  preTx.BlockRef.Seqno, LatestTransactionHash = preTx.Hash, LatestTransactionTime = preTx.Now},
        };
    }

    public  Task<string> GetBlockInfo()
    {
        throw new System.NotImplementedException();
    }

    public async Task<bool> CheckAvailable()
    {
        var url = $"{_apiConfig.Url}/addressBook?address={_tonHelper.TonOracleContractAddress}";
        await GetRequest(url);
        return true;
    }

    public Task<bool> TryGetRequestAccess()
    {
        return Task.FromResult(_requestLimit.TryGetAccess());
    }

    private async Task<HttpResponseMessage> GetRequest(string url)
    {
        var client = CreateClient();
        
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        return resp;
    }
    
    private async Task<T> GetDeserializeRequest<T>(string url)
    {
        var resp = await GetRequest(url);
        return await resp.Content.DeserializeSnakeCaseHttpContent<T>();
    }
    
    private HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        if (!string.IsNullOrEmpty(_apiConfig.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiConfig.ApiKey);
        }

        client.DefaultRequestHeaders.Add("accept", "application/json");
        
        return client;
    }
}

public class TonCenterRequestLimit
{
    private object _lock = new object();
    
    private int _perSecondLimit;

    private long _latestExecuteTime;

    private int _latestSecondExecuteCount;

    public TonCenterRequestLimit(int perSecondLimit)
    {
        _perSecondLimit = perSecondLimit;
    }

    public bool TryGetAccess()
    {
        var dtNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        lock (_lock)
        {
            if (_latestExecuteTime == dtNow )
            {
                if (_perSecondLimit >= _latestSecondExecuteCount)
                {
                    return false;
                }
    
                _latestSecondExecuteCount += 1;
                return true;
            }

            if (dtNow > _latestExecuteTime)
            {
                _latestExecuteTime = dtNow;
                _latestSecondExecuteCount = 1;
            }
            
            return true;
        }
    }
}

public class TransactionsResponse
{
    public List<Transaction> Transactions { get; set; }
}

public class Transaction
{
    public string Account { get; set; }
    public AccountState AccountStateAfter { get; set; }
    public AccountState AccountStateBefore { get; set; }
    public BlockId BlockRef { get; set; }
    public TransactionDescr Description { get; set; }
    public string EndStatus { get; set; }
    public string Hash { get; set; }
    public Message InMsg { get; set; }
    public int McBlockSeqno { get; set; }
    public int Now { get; set; }
    public string OrigStatus { get; set; }
    public List<Message> OutMsgs { get; set; }
    public string PrevTransHash { get; set; }
    public string PrevTransLt { get; set; }
    public string TotalFees { get; set; }
    public string TraceId { get; set; }
}

public class AccountState
{
    public string AccountStatus { get; set; }
    public string Balance { get; set; }
    public string CodeBoc { get; set; }
    public string CodeHash { get; set; }
    public string DataBoc { get; set; }
    public string DataHash { get; set; }
    public string FrozenHash { get; set; }
    public string Hash { get; set; }
}

public class BlockId
{
    public Int64 Seqno { get; set; }
    
    public string Shard { get; set; }
    
    public int Workchain { get; set; }
}

public class TransactionDescr
{
    public bool Aborted { get; set; }
    public ActionPhase Action { get; set; }
    public BouncePhase Bounce { get; set; }
    public ComputePhase ComputePh { get; set; }
    public bool CreditFirst { get; set; }
    public CreditPhase CreditPh { get; set; }
    public bool Destroyed { get; set; }
    public bool Installed { get; set; }
    public bool IsTock { get; set; }
    public SplitInfo SplitInfo { get; set; }
    public StoragePhase StoragePh { get; set; }
    public string Type { get; set; }
}

public class Message
{
    public bool Bounce { get; set; }
    public bool Bounced { get; set; }
    public string CreatedAt { get; set; }
    public string CreatedLt { get; set; }
    public string Destination { get; set; }
    public string FwdFee { get; set; }
    public string Hash { get; set; }
    public bool IhrDisabled { get; set; }
    public string IhrFee { get; set; }
    public string ImportFee { get; set; }
    public MessageContent InitState { get; set; }
    public MessageContent MessageContent { get; set; }
    public int Opcode { get; set; }
    public string Source { get; set; }
    public string Value { get; set; }
}

public class MessageContent
{
    public string Body { get; set; }
    public DecodedContent Decoded { get; set; }
    public string Hash { get; set; }
}

public class DecodedContent
{
    public string Comment { get; set; }
    public string Type { get; set; }
}

public class ActionPhase
{
    public string ActionListHash { get; set; }
    public int MsgsCreated { get; set; }
    public bool NoFunds { get; set; }
    public int ResultArg { get; set; }
    public int ResultCode { get; set; }
    public int SkippedActions { get; set; }
    public int SpecActions { get; set; }
    public string StatusChange { get; set; }
    public bool Success { get; set; }
    public int TotActions { get; set; }
    public MessageSize TotMsgSize { get; set; }
    public string TotalActionFees { get; set; }
    public string TotalFwdFees { get; set; }
    public bool Valid { get; set; }
}

public class BouncePhase
{
    public string FwdFees { get; set; }
    public string MsgFees { get; set; }
    public MessageSize MsgSize { get; set; }
    public string ReqFwdFees { get; set; }
    public string Type { get; set; }
}

public class ComputePhase
{
    public  bool AccountActivated { get; set; }
    public int ExitArg { get; set; }
    public int ExitCode { get; set; }
    public string GasCredit { get; set; }
    public string GasFee { get; set; }
    public string GasLimit { get; set; }
    public string GasUsed { get; set; }
    public int Mode { get; set; }
    public bool MsgStateUsed { get; set; }
    public string Reason { get; set; }
    public bool Skipped { get; set; }
    public bool Success { get; set; }
    public string VmFinalStateHash { get; set; }
    public string VmInitStateHash { get; set; }
    public int VmSteps { get; set; }
}

public class CreditPhase
{
 public string Credit { get; set; }
 public string DueFeesCollected { get; set; }
}

public class SplitInfo
{
    public int AccSplitDepth { get; set; }
    public int CurShardPfxLen { get; set; }
    public string SiblingAddr { get; set; }
    public string ThisAddr { get; set; }
}

public class StoragePhase
{
    public string StatusChange { get; set; }
    public string StorageFeesCollected { get; set; }
    public string StorageFeesDue { get; set; }
}

public class MessageSize
{
    public string Bits { get; set; }
    public string Cell { get; set; }
}

public class TonCenterApiConfig
{
    public string Url { get; set; }
    public int Weight { get; set; }
    public string ApiKey { get; set; }
    
    public int ApiKeyPerSecondRequestLimit { get; set; }
    
    public int NoApiKeyPerSecondRequestLimit { get; set; }
}