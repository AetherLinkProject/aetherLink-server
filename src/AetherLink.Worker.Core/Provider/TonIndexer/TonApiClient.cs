using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TonSdk.Core;
using TonSdk.Core.Boc;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider.TonIndexer;

public class TonApiClient : TonIndexerBase, ISingletonDependency
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly TonapiProviderApiConfig _tonapiProviderApiConfig;
    private readonly TonPublicOptions _tonPublicOptions;
    private TonapiRequestLimit _tonapiRequestLimit;
    private readonly ILogger<TonApiClient> _logger;

    public TonApiClient(IOptionsSnapshot<TonapiProviderApiConfig> snapshotConfig,
        IOptionsSnapshot<TonPublicOptions> tonPublicOptions,
        IHttpClientFactory clientFactory, ILogger<TonApiClient> logger) : base(
        tonPublicOptions, logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
        _tonPublicOptions = tonPublicOptions.Value;
        _tonapiProviderApiConfig = snapshotConfig.Value;

        var limitCount = string.IsNullOrWhiteSpace(_tonapiProviderApiConfig.ApiKey)
            ? _tonapiProviderApiConfig.NoApiKeyPerSecondRequestLimit
            : _tonapiProviderApiConfig.ApiKeyPerSecondRequestLimit;
        _tonapiRequestLimit = new TonapiRequestLimit(limitCount);

        ApiWeight = _tonapiProviderApiConfig.Weight;
        ProviderName = TonStringConstants.TonApi;
    }

    public override async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        var path = $"/v2/blockchain/transactions/{txId}";
        var transactions = await GetDeserializeRequest<TonApiTransaction>(path);

        return string.IsNullOrWhiteSpace(transactions.Hash) ? null : transactions.ConvertTransactionDto();
    }

    public override async Task<(List<CrossChainToTonTransactionDto>, TonIndexerDto)> GetSubsequentTransaction(
        TonIndexerDto tonIndexerDto)
    {
        var path =
            $"/v2/blockchain/accounts/{_tonPublicOptions.ContractAddress}/transactions?after_lt={tonIndexerDto.LatestTransactionLt}&limit=30&sort_order=asc";
        var transactionResp = await GetDeserializeRequest<TonApiTransactions>(path);

        CrossChainToTonTransactionDto preTx = null;

        var result = new List<CrossChainToTonTransactionDto>();
        var skipCount = tonIndexerDto.SkipCount;
        var latestTransactionLt = tonIndexerDto.LatestTransactionLt;

        // transactions has been order by asc 
        foreach (var originalTx in transactionResp.Transactions)
        {
            if (originalTx.Hash == tonIndexerDto.LatestTransactionHash)
            {
                continue;
            }

            var tx = originalTx.ConvertTransactionDto();
            preTx = tx;

            if (tx.TransactionLt == latestTransactionLt)
            {
                skipCount += 1;
            }
            else
            {
                latestTransactionLt = tx.TransactionLt;
                skipCount = 0;
            }

            if (tx.OpCode == TonOpCodeConstants.ForwardTx || tx.OpCode == TonOpCodeConstants.ResendTx ||
                tx.OpCode == TonOpCodeConstants.ReceiveTx)
            {
                result.Add(tx);
            }
        }

        return
            (result,
                preTx == null
                    ? tonIndexerDto
                    : new TonIndexerDto()
                    {
                        BlockHeight = preTx.SeqNo, LatestTransactionHash = preTx.Hash,
                        LatestTransactionLt = preTx.TransactionLt,
                        SkipCount = skipCount
                    });
    }

    public override async Task<string> CommitTransaction(Cell bodyCell)
    {
        var path = "/v2/blockchain/message";
        var body = new Dictionary<string, string>()
        {
            {
                "boc",
                bodyCell.ToString("base64")
            }
        };

        try
        {
            var respStr = await PostRequest(path, JsonConvert.SerializeObject(body));

            if (string.IsNullOrWhiteSpace(respStr))
            {
                return null;
            }

            // judge json string
            if (respStr.StartsWith("{"))
            {
                var errorDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(respStr);
                errorDic.TryGetValue(TonStringConstants.Error, out string errorMsg);
                _logger.LogWarning($"Tonapi  Commit Transaction error, message is:{errorMsg}");
                return null;
            }

            return respStr;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Ton Api Provider] Send Transaction error:{ex}");
            throw;
        }
    }

    public override Task<TonBlockInfo> GetLatestBlockInfo()
    {
        throw new NotImplementedException("Tonapi not support GetLatestBlockInfo");
    }

    public override async Task<uint?> GetAddressSeqno(Address address)
    {
        var path = $"/v2/wallet/{address.ToString()}/seqno";
        var respDic = await GetDeserializeRequest<Dictionary<String, UInt32>>(path);
        return respDic.TryGetValue(TonStringConstants.Seqno, out var seqno) ? seqno : (uint?)0;
    }

    public override async Task<bool> CheckAvailable()
    {
        var path =
            $"/v2/blockchain/accounts/{_tonPublicOptions.ContractAddress}/transactions?after_lt=0&limit=1&sort_order=asc";
        await GetDeserializeRequest<TonApiTransactions>(path);
        return true;
    }

    public override Task<bool> TryGetRequestAccess()
    {
        return Task.FromResult(_tonapiRequestLimit.TryGetAccess());
    }

    protected override string AssemblyUrl(string path)
    {
        return
            $"{_tonapiProviderApiConfig.Url}{(_tonapiProviderApiConfig.Url.EndsWith("/") ? "" : "/")}{(path.StartsWith("/") ? path.Substring(1) : path)}";
    }

    protected override HttpClient CreateClient()
    {
        var client = _clientFactory.CreateClient();
        if (!string.IsNullOrEmpty(_tonapiProviderApiConfig.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", _tonapiProviderApiConfig.ApiKey);
        }

        return client;
    }
}

public class TonapiRequestLimit
{
    private readonly object _lock = new object();
    private long _latestExecuteTime;
    private int _latestSecondExecuteCount;
    private int _perSecondLimit;

    public TonapiRequestLimit(int perSecondLimit)
    {
        _perSecondLimit = perSecondLimit;
    }

    public bool TryGetAccess()
    {
        var dtNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        lock (_lock)
        {
            if (_latestExecuteTime == dtNow)
            {
                if (_perSecondLimit <= _latestSecondExecuteCount)
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

public class TonApiTransactions
{
    public List<TonApiTransaction> Transactions { get; set; }
}

public class TonApiTransaction
{
    public string Hash { get; set; }
    public long Lt { get; set; }
    public TonApiAccount Account { get; set; }
    public bool Success { get; set; }
    public long Utime { get; set; }
    public string OrigStatus { get; set; }
    public string EndStatus { get; set; }
    public long TotalFees { get; set; }
    public long EndBalance { get; set; }
    public string TransactionType { get; set; }
    public string StateUpdateOld { get; set; }
    public string StateUpdateNew { get; set; }
    public TonapiMessage InMsg { get; set; }
    public List<TonapiMessage> OutMsgs { get; set; }
    public string Block { get; set; }
    public string PrevTransHash { get; set; }
    public long PrevTransLt { get; set; }
    public TonapiComputePhase ComputePhase { get; set; }
    public TonapiStoragePhase StoragePhase { get; set; }
    public TonapiCreditPhase CreditPhase { get; set; }
    public TonapiActionPhase ActionPhase { get; set; }
    public string BouncePhase { get; set; }
    public bool Aborted { get; set; }
    public bool Destroyed { get; set; }
    public string Raw { get; set; }

    public CrossChainToTonTransactionDto ConvertTransactionDto()
    {
        var blockStr = Block.Replace("(", "").Replace(")", "").Split(",");

        string outMsg = null;
        if (OutMsgs != null && OutMsgs.Count > 0)
        {
            outMsg = OutMsgs[0].RawBody;
        }

        return new CrossChainToTonTransactionDto()
        {
            WorkChain = int.Parse(blockStr[0]),
            Shard = blockStr[1],
            SeqNo = long.Parse(blockStr[2]),
            TraceId = Hash,
            Hash = Hash,
            PrevHash = PrevTransHash,
            BlockTime = Utime,
            TransactionLt = Lt.ToString(),
            OpCode = InMsg.OpCode == null ? 0 : Convert.ToInt32(InMsg.OpCode, 16),
            Body = InMsg.RawBody,
            Success = ActionPhase?.Success ?? false,
            ExitCode = ComputePhase.ExitCode,
            Aborted = Aborted,
            Bounce = InMsg.Bounce ?? false,
            Bounced = InMsg.Bounced ?? false,
            OutMessage = outMsg
        };
    }
}

public class TonApiAccount
{
    public string Address { get; set; }
    public string Name { get; set; }
    public bool IsScam { get; set; }
    public string Icon { get; set; }
    public bool IsWallet { get; set; }
}

public class TonapiMessage
{
    public string MsgType { get; set; }
    public long CreatedLt { get; set; }
    public bool? IhrDisabled { get; set; }
    public bool? Bounce { get; set; }
    public bool? Bounced { get; set; }
    public long Value { get; set; }
    public long FwdFee { get; set; }
    public long IhrFee { get; set; }
    public TonApiAccount Destination { get; set; }
    public TonApiAccount Source { get; set; }
    public long ImportFee { get; set; }
    public long CreatedAt { get; set; }
    public string OpCode { get; set; }
    public Init Init { get; set; }
    public string Hash { get; set; }
    public string RawBody { get; set; }
    public string DecodedOpName { get; set; }
    public DecodedBody DecodedBody { get; set; }
}

public class Init
{
    public string Boc { get; set; }
    public List<string> Interfaces { get; set; }
}

public class TonapiComputePhase
{
    public bool Skipped { get; set; }
    public string SkipReason { get; set; }
    public bool Success { get; set; }
    public long GasFees { get; set; }
    public long GasUsed { get; set; }
    public long VmSteps { get; set; }
    public int ExitCode { get; set; }
    public string ExitCodeDescription { get; set; }
}

public class TonapiStoragePhase
{
    public long FeesCollected { get; set; }
    public long FeesDue { get; set; }
    public string StatusChange { get; set; }
}

public class TonapiCreditPhase
{
    public long FeesCollected { get; set; }
    public long Credit { get; set; }
}

public class TonapiActionPhase
{
    public bool Success { get; set; }
    public int ResultCode { get; set; }
    public int TotalActions { get; set; }
    public int SkippedActions { get; set; }
    public long FwdFees { get; set; }
    public long TotalFees { get; set; }
    public string ResultCodeDescription { get; set; }
}

public class DecodedBody
{
    public string Payload { get; set; }
}