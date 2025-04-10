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
            _logger.LogDebug($"[TonCenterApiProvider] Search transaction from {latestTransactionLt}");

            var latestBlockInfo = await _storageProvider.GetTonCenterLatestBlockInfoAsync();
            if (latestBlockInfo == null)
            {
                _logger.LogDebug("[TonCenterApiProvider] Waiting for timer sync ton latest block info.");
                return new();
            }

            if (latestBlockInfo.McBlockSeqno <= latestBlockHeight + _option.TransactionsSubscribeDelay)
            {
                _logger.LogDebug("[TonCenterApiProvider] Waiting for Ton latest block.");
                return new();
            }

            var path =
                $"/api/v3/transactions?account={contractAddress}&start_lt={latestTransactionLt}&limit=100&offset=0&sort=asc";

            var client = _clientFactory.CreateClient();
            if (!string.IsNullOrEmpty(_option.ApiKey)) client.DefaultRequestHeaders.Add("X-Api-Key", _option.ApiKey);
            var responseMessage = await client.GetAsync(_option.Url + path);
            var result = await responseMessage.Content.DeserializeSnakeCaseHttpContent<TransactionsResponse>();

            return result.Transactions.Where(tx =>
                    tx.McBlockSeqno <= latestBlockInfo.McBlockSeqno - _option.TransactionsSubscribeDelay)
                .Select(t => t.ConvertToTonTransactionDto()).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TonCenterApiProvider] Subscribe Ton transaction failed");
            return new();
        }
    }

    public async Task<MasterChainInfoDto> GetCurrentHighestBlockHeightAsync()
    {
        try
        {
            var responseMessage = await _clientFactory.CreateClient().GetAsync(_option.Url + "/api/v3/masterchainInfo");
            return await responseMessage.Content.DeserializeSnakeCaseHttpContent<MasterChainInfoDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TonCenterApiProvider] Get current highest block info failed");
            throw;
        }
    }

    public async Task<string> CommitTransaction(Cell bodyCell)
    {
        var body = new Dictionary<string, string> { { "boc", bodyCell.ToString("base64") } };
        try
        {
            var resp = await _clientFactory.CreateClient().PostAsync("/sendBoc",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.Default, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[TonCenterApiProvider] Send Commit failed");
                return null;
            }

            var result = await resp.Content.DeserializeSnakeCaseHttpContent<SendBocResult>();
            if (!string.IsNullOrEmpty(result.Hash)) return result.Hash;

            _logger.LogWarning("[TonCenterApiProvider] Send Commit failed");

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TonCenterApiProvider] Send Transaction error");
            throw;
        }
    }

    public async Task<uint?> GetAddressSeqno(Address address)
    {
        try
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
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[TonCenterApiProvider] Get Address Seqno error");
            throw;
        }
    }
}