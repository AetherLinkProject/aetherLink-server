using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Volo.Abp.DependencyInjection;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AetherLink.Indexer.Provider;

public interface ITonIndexerProvider
{
    public Task<List<TonTransactionDto>> SubscribeTransactionAsync(string latestTransactionLt);
}

public class TonIndexerProvider : ITonIndexerProvider, ITransientDependency
{
    private readonly TonIndexerOption _option;
    private readonly IHttpClientService _httpClient;
    private readonly ILogger<TonIndexerProvider> _logger;

    public TonIndexerProvider(IOptionsSnapshot<TonIndexerOption> tonIndexerOption, IHttpClientService httpClient,
        ILogger<TonIndexerProvider> logger)
    {
        _logger = logger;
        _httpClient = httpClient;
        _option = tonIndexerOption.Value;
    }

    public async Task<List<TonTransactionDto>> SubscribeTransactionAsync(string latestTransactionLt)
    {
        try
        {
            var path =
                $"/api/v3/transactions?account={_option.ContractAddress}&start_lt={latestTransactionLt}&limit=30&sort_order=asc";
            var resultStr = await _httpClient.GetAsync(_option.Url + path, new());

            var serializeSetting = new JsonSerializerSettings
                { ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() } };
            var result =
                JsonConvert.DeserializeObject<TonCenterGetTransactionsResponseDto>(resultStr, serializeSetting);

            _logger.LogDebug($"[TonIndexerProvider] {JsonSerializer.Serialize(result)}");
            // var skipCount = tonIndexerDto.SkipCount;
            // var latestTransactionLt = tonIndexerDto.LatestTransactionLt;

            // transactions has been order by asc 
            // foreach (var transaction in result.Transactions)
            // {
            //     if (transaction.Hash == tonIndexerDto.LatestTransactionHash) continue;
            //     // var opCode = string.IsNullOrEmpty(transaction.InMsg.OpCode)
            //     //     ? 0
            //     //     : Convert.ToInt32(transaction.InMsg.OpCode, 16);
            //     latestTransactionLt = transaction.Lt.ToString() == latestTransactionLt
            //         ? latestTransactionLt
            //         : transaction.Lt.ToString();
            //     skipCount = transaction.Lt.ToString() == latestTransactionLt ? skipCount + 1 : 0;
            // }

            return result.Transactions;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Subscribe Ton transaction failed");
            return new();
        }
    }
}