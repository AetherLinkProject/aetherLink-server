using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using JetBrains.Annotations;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public abstract class TonIndexerBase
{
    private readonly TonHelper _tonHelper;
    protected int _apiWeight { get; set; }
    public int ApiWeight => _apiWeight;

    protected TonIndexerBase(TonHelper tonHelper)
    {
        _tonHelper = tonHelper;
    }
    
    [ItemCanBeNull]
    public virtual async Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId)
    {
        var path = $"transactions?hash={txId}";
        var transactions = await GetDeserializeRequest<TransactionsResponse>(path);

        if (transactions.Transactions.Count == 0)
        {
            return null;
        }

        return transactions.Transactions[0].ConvertToTonTransactionDto();
    }

    public virtual async Task<(List<CrossChainToTonTransactionDto>, TonIndexerDto)> GetSubsequentTransaction(
        TonIndexerDto tonIndexerDto)
    {
        var path = $"transactions?account={_tonHelper.TonOracleContractAddress}&start_lt={tonIndexerDto.LatestTransactionLt}&limit=30&offset={tonIndexerDto.SkipCount}&sort=asc";
        var transactionResp = await GetDeserializeRequest<TransactionsResponse>(path);
        
        var preHash = tonIndexerDto.LatestTransactionHash;
        Transaction preTx = null;

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
            
            preTx = originalTx;

            if (originalTx.Lt == latestTransactionLt)
            {
                skipCount += 1;
            }
            else
            {
                skipCount = 0;
            }
            
            var tx = originalTx.ConvertToTonTransactionDto();
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
                        BlockHeight = preTx.BlockRef.Seqno, LatestTransactionHash = preTx.Hash,
                        LatestTransactionLt = preTx.Lt,
                        SkipCount = skipCount
                    });
    }

    public virtual async Task<TonBlockInfo> GetLatestBlockInfo()
    {
        var path = "blocks?workchain=0&limit=1&offset=0&sort=desc";
        var blocks = await GetDeserializeRequest<TonBlocks>(path);
        if (blocks.Blocks.Count == 0)
        {
            return null;
        }

        return blocks.Blocks[0];
    }

    public virtual async Task<bool> CheckAvailable()
    {
        var url = $"/addressBook?address={_tonHelper.TonOracleContractAddress}";
        await GetRequest(url);
        return true;
    }
    
    public abstract Task<bool> TryGetRequestAccess();

    protected async Task<T> GetDeserializeRequest<T>(string path)
    {
        var resp = await GetRequest(path);
        return await resp.Content.DeserializeSnakeCaseHttpContent<T>();
    }
    
    protected virtual async Task<HttpResponseMessage> GetRequest(string path)
    {
        var client = CreateClient();
        var url = AssemblyUrl(path);
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException();
        }

        return resp;
    }

    protected abstract string AssemblyUrl(string path);

    protected abstract HttpClient CreateClient();
}