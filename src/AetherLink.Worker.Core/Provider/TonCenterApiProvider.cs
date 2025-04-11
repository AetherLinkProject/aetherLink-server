using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int TransactionFetchPageLimit = 100;

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
                _logger.LogDebug(
                    $"[TonCenterApiProvider] Search transaction from blockHeight: {latestBlockHeight} lt: {latestTransactionLt}");

                var allTransactions = new List<CrossChainToTonTransactionDto>();
                var latestBlockInfo = await _storageProvider.GetTonCenterLatestBlockInfoAsync();
                if (latestBlockInfo == null)
                {
                    _logger.LogDebug("[TonCenterApiProvider] Waiting for timer sync ton latest block info.");
                    return allTransactions;
                }

                var skipCount = 0;
                int totalFetched;
                var client = GetApiKeyClient();

                do
                {
                    var path =
                        $"{TonHttpApiUriConstants.GetTransactions}?account={contractAddress}&start_lt={latestTransactionLt}&TransactionFetchPageLimit={TransactionFetchPageLimit}&offset={skipCount}&sort=asc";

                    _logger.LogDebug($"[TonCenterApiProvider] Fetching with uri: {path}, offset: {skipCount}");

                    var responseMessage = await client.GetAsync(_option.Url + path);
                    var result = await responseMessage.Content.DeserializeSnakeCaseHttpContent<TransactionsResponse>();

                    var filteredTransactions = result.Transactions.Where(tx =>
                            tx.McBlockSeqno + _option.TransactionsSubscribeDelay <= latestBlockInfo.McBlockSeqno)
                        .Select(t => t.ConvertToTonTransactionDto()).ToList();
                    allTransactions.AddRange(filteredTransactions);
                    totalFetched = result.Transactions.Count;
                    skipCount += totalFetched;

                    _logger.LogDebug($"[TonCenterApiProvider] Fetched {totalFetched} transactions");

                    // todo test add log for filter
                    var filteredCount = result.Transactions.Count(tx =>
                        tx.McBlockSeqno + _option.TransactionsSubscribeDelay > latestBlockInfo.McBlockSeqno);
                    var nextBlockSeqno = result.Transactions.Where(tx =>
                            tx.McBlockSeqno + _option.TransactionsSubscribeDelay > latestBlockInfo.McBlockSeqno)
                        .MinBy(tx => tx.McBlockSeqno)?.McBlockSeqno;
                    _logger.LogInformation(
                        "[TonCenterApiProvider] Filtered transactions count: {FilteredCount}, Next transaction's minimum McBlockSeqno exceeding limit: {NextBlockSeqno}",
                        filteredCount,
                        nextBlockSeqno.HasValue ? nextBlockSeqno.Value.ToString() : "None"
                    );
                } while (totalFetched == TransactionFetchPageLimit);

                return allTransactions;
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
            var responseMessage = await _clientFactory.CreateClient()
                .GetAsync(_option.Url + TonHttpApiUriConstants.MasterChainInfo);
            return await responseMessage.Content.DeserializeSnakeCaseHttpContent<MasterChainInfoDto>();
        }, "GetCurrentHighestBlockHeightAsync");
    }

    public async Task<string> CommitTransaction(Cell bodyCell)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var bodyString = JsonConvert.SerializeObject(
                new Dictionary<string, string> { { "boc", bodyCell.ToString("base64") } }
            );

            _logger.LogWarning($"[TonCenterApiProvider] Send Commit body string: {bodyString}");

            var resp = await GetApiKeyClient().PostAsync(_option.Url + TonHttpApiUriConstants.SendTransaction,
                new StringContent(bodyString, Encoding.Default, "application/json"));

            var result = await resp.Content.DeserializeSnakeCaseHttpContent<SendTransactionResultDto>();
            if (result.Ok) return bodyCell.Hash.ToString("base64");

            _logger.LogWarning($"[TonCenterApiProvider] Send Commit failed, error: {result.Error}");

            return null;
        }, "CommitTransaction");
    }

    public async Task<uint?> GetAddressSeqno(Address address)
    {
        return await ExecuteWithRetryAsync<uint?>(async () =>
        {
            var bodyString = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "address", address.ToString() },
                { "method", "seqno" },
                { "stack", Array.Empty<string[]>() }
            });

            var resp = await GetApiKeyClient().PostAsync(_option.Url + TonHttpApiUriConstants.RunGetMethod,
                new StringContent(bodyString, Encoding.Default, "application/json"));
            var method = await resp.Content.DeserializeSnakeCaseHttpContent<RunGetMethodResult>();
            if (method.ExitCode != 0 && method.ExitCode != 1)
            {
                _logger.LogWarning($"[TonCenterApiProvider] Get not expected exit code: {method.ExitCode}");
                return 0;
            }

            if (method.Stack == null || method.Stack.Length == 0)
            {
                _logger.LogWarning("[TonCenterApiProvider] Get empty stack.");
                return 0;
            }

            var value = method.Stack[0].ToString();
            if (value == null) return 0;
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
            return Convert.ToUInt32(data[TonStringConstants.Value], 16);
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

    private HttpClient GetApiKeyClient()
    {
        var client = _clientFactory.CreateClient();

        if (!string.IsNullOrEmpty(_option.ApiKey)) client.DefaultRequestHeaders.Add("X-Api-Key", _option.ApiKey);

        client.DefaultRequestHeaders.Add("accept", "application/json");

        return client;
    }
}