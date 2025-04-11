using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TonSdk.Client;
using TonSdk.Core;
using TonSdk.Core.Boc;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITonCenterApiProvider
{
    Task<List<CrossChainToTonTransactionDto>> SubscribeTransactionAsync(string contractAddress,
        string latestTransactionLt, long latestBlockHeight);

    Task<MasterChainInfoDto> GetCurrentHighestBlockHeightAsync();
    Task<string> CommitTransaction(Cell bodyCell);
    Task<uint?> GetAddressSeqno(Address address);
}

public class TonCenterApiProvider : ITonCenterApiProvider, ISingletonDependency
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly TonCenterProviderApiConfig _option;
    private readonly ITonStorageProvider _storageProvider;
    private readonly ILogger<TonCenterApiProvider> _logger;

    public TonCenterApiProvider(IOptionsSnapshot<TonCenterProviderApiConfig> option,
        ILogger<TonCenterApiProvider> logger, IHttpClientFactory clientFactory, ITonStorageProvider storageProvider)
    {
        _logger = logger;
        _option = option.Value;
        _clientFactory = clientFactory;
        _storageProvider = storageProvider;
    }

    public async Task<List<CrossChainToTonTransactionDto>> SubscribeTransactionAsync(string contractAddress,
        string latestTransactionLt, long latestBlockHeight)
    {
        try
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogDebug($"[TonCenterApiProvider] Search transaction from {latestTransactionLt}");

                var latestBlockInfo = await _storageProvider.GetTonCenterLatestBlockInfoAsync();
                if (latestBlockInfo == null)
                {
                    _logger.LogDebug("[TonCenterApiProvider] Waiting for timer sync ton latest block info.");
                    return new List<CrossChainToTonTransactionDto>();
                }

                if (latestBlockInfo.McBlockSeqno <= latestBlockHeight + _option.TransactionsSubscribeDelay)
                {
                    _logger.LogDebug(
                        $"[TonCenterApiProvider] Current block height: {latestBlockInfo.McBlockSeqno},Waiting for Ton latest block.");
                    return new();
                }

                var path =
                    $"/api/v3/transactions?account={contractAddress}&start_lt={latestTransactionLt}&limit=100&offset=0&sort=asc";
                var client = _clientFactory.CreateClient();
                if (!string.IsNullOrEmpty(_option.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Api-Key", _option.ApiKey);
                var responseMessage = await client.GetAsync(_option.Url + path);
                var result = await responseMessage.Content.DeserializeSnakeCaseHttpContent<TransactionsResponse>();
                return result.Transactions
                    .Where(tx => tx.McBlockSeqno <= latestBlockInfo.McBlockSeqno - _option.TransactionsSubscribeDelay)
                    .Select(t => t.ConvertToTonTransactionDto()).ToList();
            }, "SubscribeTransactionAsync");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TonCenterApiProvider] Subscribe Ton transaction failed");
            return new();
        }
    }

    public async Task<MasterChainInfoDto> GetCurrentHighestBlockHeightAsync()
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var responseMessage = await _clientFactory.CreateClient().GetAsync(_option.Url + "/api/v3/masterchainInfo");
            return await responseMessage.Content.DeserializeSnakeCaseHttpContent<MasterChainInfoDto>();
        }, "GetCurrentHighestBlockHeightAsync");
    }

    public async Task<string> CommitTransaction(Cell bodyCell)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var body = new Dictionary<string, string> { { "boc", bodyCell.ToString("base64") } };
            var resp = await _clientFactory.CreateClient().PostAsync("/sendBoc",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.Default, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[TonCenterApiProvider] Send Commit failed");
                return null;
            }

            var result = await resp.Content.DeserializeSnakeCaseHttpContent<SendBocResult>();
            return !string.IsNullOrEmpty(result.Hash) ? result.Hash : null;
        }, "CommitTransaction");
    }

    public async Task<uint?> GetAddressSeqno(Address address)
    {
        return await ExecuteWithRetryAsync<uint?>(async () =>
        {
            var body = new Dictionary<string, object>
            {
                { "address", address.ToString() },
                { "method", "seqno" },
                { "stack", Array.Empty<string[]>() }
            };
            var resp = await _clientFactory.CreateClient().PostAsync(TonStringConstants.RunGetMethod,
                new StringContent(JsonConvert.SerializeObject(body), Encoding.Default, "application/json"));
            var method = await resp.Content.DeserializeSnakeCaseHttpContent<RunGetMethodResult>();
            if (method.ExitCode != 0 && method.ExitCode != 1) return 0;
            var value = method.Stack[0].ToString();
            if (value == null) return 0;
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
            var num = Convert.ToUInt32(data[TonStringConstants.Value], 16);
            return num;
        }, "GetAddressSeqno");
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationDescription,
        int maxRetries = 10, int delayInSeconds = 10)
    {
        var retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException he)
            {
                _logger.LogWarning(he,
                    "[TonCenterApiProvider] {OperationDescription} Retrying in {DelaySeconds} seconds... (Attempt {RetryCount}/{MaxRetries})",
                    operationDescription, delayInSeconds, retryCount + 1, maxRetries);
                retryCount++;

                await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[TonCenterApiProvider] {OperationDescription} failed on attempt {RetryCount}/{MaxRetries}",
                    operationDescription, retryCount + 1, maxRetries);
                throw;
            }
        }

        throw new Exception(
            $"[TonCenterApiProvider] {operationDescription} - Exceeded max retry attempts ({maxRetries}).");
    }
}