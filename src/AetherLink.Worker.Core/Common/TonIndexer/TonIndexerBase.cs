using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TonSdk.Client;
using TonSdk.Core;
using TonSdk.Core.Boc;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public interface ITonIndexerProvider
{
    int Weight { get; }
    string ApiProviderName { get; }
    Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId);

    Task<(List<CrossChainToTonTransactionDto>, TonIndexerDto)> GetSubsequentTransaction(
        TonIndexerDto tonIndexerDto);

    Task<TonBlockInfo> GetLatestBlockInfo();

    Task<uint?> GetAddressSeqno(Address address);

    Task<string> CommitTransaction(Cell bodyCell);

    Task<bool> CheckAvailable();

    Task<bool> TryGetRequestAccess();
}

public abstract class TonIndexerBase : ITonIndexerProvider
{
    private readonly string _contractAddress;
    private readonly ILogger<TonIndexerBase> _logger;
    protected int ApiWeight { get; init; }
    protected string ProviderName { get; init; }
    public int Weight => ApiWeight;
    public string ApiProviderName => ProviderName;

    protected TonIndexerBase(IOptionsSnapshot<TonPublicConfigOptions> tonPublicOptions, ILogger<TonIndexerBase> logger)
    {
        _logger = logger;
        _contractAddress = tonPublicOptions.Value.ContractAddress;
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
        var path =
            $"transactions?account={_contractAddress}&start_lt={tonIndexerDto.LatestTransactionLt}&limit=30&offset={tonIndexerDto.SkipCount}&sort=asc";
        var transactionResp = await GetDeserializeRequest<TransactionsResponse>(path);

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
                latestTransactionLt = originalTx.Lt;
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

    public virtual async Task<uint?> GetAddressSeqno(Address address)
    {
        var body = new Dictionary<string, object>()
        {
            {
                "address",
                address.ToString()
            },
            {
                "method",
                "seqno"
            },
            {
                "stack",
                Array.Empty<string[]>()
            }
        };

        var method =
            await PostDeserializeRequest<RunGetMethodResult?>(TonStringConstants.RunGetMethod,
                JsonConvert.SerializeObject(body));
        if (!method.HasValue)
            return 0;
        if (method.Value.ExitCode != 0 && method.Value.ExitCode != 1)
            return 0;
        var value = method.Value.Stack[0].ToString();
        if (value == null)
        {
            return 0;
        }

        var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
        var num = Convert.ToUInt32(data[TonStringConstants.Value], 16);

        return num;
    }

    public virtual async Task<string> CommitTransaction(Cell bodyCell)
    {
        var path = "/message";
        var body = new Dictionary<string, string>()
        {
            {
                "boc",
                bodyCell.ToString("base64")
            }
        };
        try
        {
            var result =
                await PostDeserializeRequest<Dictionary<String, String>>(path, JsonConvert.SerializeObject(body));
            return result.TryGetValue(TonStringConstants.MessageValue, out var transaction) ? transaction : null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Ton Api Provider] Send Transaction error:{ex}");
        }

        return null;
    }

    public virtual async Task<bool> CheckAvailable()
    {
        var url = $"/addressBook?address={_contractAddress}";
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
            _logger.LogError($"[Ton provider]  request Error, response message is:{resp}");
            throw new HttpRequestException();
        }

        return resp;
    }

    protected virtual async Task<T> PostDeserializeRequest<T>(string path, string body)
    {
        var resp = await PostMessage(path, body);
        return await resp.Content.DeserializeSnakeCaseHttpContent<T>();
    }

    protected virtual async Task<string> PostRequest(string path, string body)
    {
        var resp = await PostMessage(path, body);
        var result = await resp.Content.ReadAsStringAsync();
        return result;
    }

    protected virtual async Task<HttpResponseMessage> PostMessage(string path, string body)
    {
        var client = CreateClient();
        var url = AssemblyUrl(path);

        var resp = await client.PostAsync(url, new StringContent(body, Encoding.Default, "application/json"));
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError($"[Ton Post Message] response error, message is:{resp} ");
            throw new HttpRequestException();
        }

        return resp;
    }

    protected abstract string AssemblyUrl(string path);

    protected abstract HttpClient CreateClient();
}