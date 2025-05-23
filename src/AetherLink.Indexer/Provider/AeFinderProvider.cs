using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Indexer.Dtos;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Indexer.Provider;

public interface IAeFinderProvider
{
    public Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from);
    public Task<List<RampRequestDto>> SubscribeRampRequestsAsync(string chainId, long to, long from);
    public Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from);
    public Task<List<RampCommitReportAcceptedDto>> SubscribeRampCommitReportAsync(string chainId, long to, long from);
    public Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to, long from);
    public Task<List<RampRequestCancelledDto>> SubscribeRampRequestCancelledAsync(string chainId, long to, long from);
    public Task<List<ChainItemDto>> GetChainSyncStateAsync();
    public Task<string> GetOracleConfigAsync(string chainId);
    public Task<long> GetOracleLatestEpochAsync(string chainId, long blockHeight);
    public Task<string> GetRequestCommitmentAsync(string chainId, string requestId);
    public Task<List<TransactionEventDto>> GetTransactionLogEventsAsync(string chainId, long to, long from);

    public Task<IndexerTokenSwapConfigInfo> GetTokenSwapConfigAsync(long targetChainId, long sourceChainId,
        string receiver, string tokenAddress, string symbol);

    public Task<List<RampRequestManuallyExecutedDto>> SubscribeRampRequestManuallyExecutedAsync(string chainId, long to,
        long from);
}

public class AeFinderProvider : IAeFinderProvider, ITransientDependency
{
    private readonly AeFinderOptions _options;
    private readonly IHttpClientService _httpClient;
    private readonly ILogger<AeFinderProvider> _logger;

    public AeFinderProvider(ILogger<AeFinderProvider> logger, IOptions<AeFinderOptions> options,
        IHttpClientService httpClient)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerLogEventListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                     ocrJobEvents(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                         chainId,
                         requestTypeIndex,
                         transactionId,
                         blockHeight,
                         blockHash,
                         startTime,
                         requestId
                 }
             }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.OcrJobEvents : new List<OcrLogEventDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] Subscribe Logs failed.");
            throw;
        }
    }

    public async Task<List<RampRequestDto>> SubscribeRampRequestsAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerRampRequestListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                     rampRequests(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                         chainId,
                         transactionId,
                         messageId,
                         targetChainId,
                         sourceChainId,
                         sender,
                         receiver,
                         message,
                         startTime,
                         epoch,
                         tokenTransferMetadata {
                            targetChainId,
                            symbol,
                            amount
                         }
                 }
             }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.RampRequests : new List<RampRequestDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] Subscribe ramp requests failed.");
            throw;
        }
    }

    public async Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerTransmittedListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    transmitted(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        chainId,
                        transactionId,
                        startTime,
                        epoch,
                        requestId
                }
            }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.Transmitted : new List<TransmittedDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeTransmitted failed.");
            throw;
        }
    }

    public async Task<List<RampCommitReportAcceptedDto>> SubscribeRampCommitReportAsync(string chainId, long to,
        long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerRequestCommitListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    rampCommitReport(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        sourceChainId,
                        targetChainId,
                        transactionId,
                        messageId
                }
            }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.RampCommitReport : new();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] Subscribe RampCommitted log failed.");
            throw;
        }
    }

    public async Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerRequestCancelledListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    requestCancelled(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        chainId,
                        requestId
                }
            }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.RequestCancelled : new List<RequestCancelledDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeRequestCancelled failed.");
            throw;
        }
    }

    public async Task<List<RampRequestCancelledDto>> SubscribeRampRequestCancelledAsync(string chainId, long to,
        long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerRampRequestCancelledListDto>(GetClient(),
                new()
                {
                    Query =
                        @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    rampRequestCancelled(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        messageId
                }
            }",
                    Variables = new
                    {
                        chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                    }
                });
            return indexerResult != null ? indexerResult.RampRequestCancelled : new List<RampRequestCancelledDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeRequestCancelled failed.");
            throw;
        }
    }

    public async Task<List<RampRequestManuallyExecutedDto>> SubscribeRampRequestManuallyExecutedAsync(string chainId,
        long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerRampRequestManuallyExecutedListDto>(
                GetClient(), new()
                {
                    Query =
                        @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    rampRequestManuallyExecuted(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        messageId,
                        transactionId,
                        startTime
                        }
                    }",
                    Variables = new
                    {
                        chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                    }
                });
            return indexerResult != null
                ? indexerResult.RampRequestManuallyExecuted
                : new List<RampRequestManuallyExecutedDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeRequestCancelled failed.");
            throw;
        }
    }

    public async Task<List<ChainItemDto>> GetChainSyncStateAsync()
    {
        var result = await _httpClient.GetAsync<AeFinderSyncStateDto>(_options.BaseUrl + _options.SyncStateUri, new());
        return result.CurrentVersion.Items;
    }

    public async Task<string> GetOracleConfigAsync(string chainId)
    {
        try
        {
            var res = await GraphQLHelper.SendQueryAsync<OracleConfigDigestRecord>(GetClient(), new()
            {
                Query = @"
			        query($chainId:String!){
                        oracleConfigDigest(input: {chainId:$chainId}){
                            configDigest
                    }
                }",
                Variables = new
                {
                    chainId = chainId
                }
            });

            return res?.OracleConfigDigest == null ? "" : res.OracleConfigDigest.ConfigDigest;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] GetOracleConfig failed.");
            return "";
        }
    }

    public async Task<long> GetOracleLatestEpochAsync(string chainId, long blockHeight)
    {
        try
        {
            var res = await GraphQLHelper.SendQueryAsync<OracleLatestEpochRecord>(GetClient(), new()
            {
                Query = @"
			        query($chainId:String!,$blockHeight:Long!){
                        oracleLatestEpoch(input: {chainId:$chainId,blockHeight:$blockHeight}){
                        epoch
                    }
                }",
                Variables = new
                {
                    chainId = chainId, blockHeight = blockHeight
                }
            });

            return res?.OracleLatestEpoch?.Epoch ?? 0;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] GetOracleLatestEpoch failed.");
            throw;
        }
    }

    public async Task<string> GetRequestCommitmentAsync(string chainId, string requestId)
    {
        try
        {
            var res = await GraphQLHelper.SendQueryAsync<RequestCommitmentRecord>(GetClient(), new()
            {
                Query = @"
                    query($chainId:String!,$requestId:String!){
                        requestCommitment(input: {chainId:$chainId,requestId:$requestId}){
                            chainId,
                            requestId,
                            commitment
                        }
                }",
                Variables = new
                {
                    chainId, requestId
                }
            });

            return res?.RequestCommitment == null ? "" : res.RequestCommitment.Commitment;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] GetRequestCommitment failed.");
            return "";
        }
    }

    public async Task<List<TransactionEventDto>> GetTransactionLogEventsAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerTransactionEventListDto>(GetClient(), new()
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    transactionEvents(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                            chainId,
                            blockHash,
                            transactionId,
                            blockHeight,
                            methodName,
                            contractAddress,
                            eventName,
                            index,
                            startTime
                    }
                }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult == null ? new() : indexerResult.TransactionEvents;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeLogs failed.");
            throw;
        }
    }

    public async Task<IndexerTokenSwapConfigInfo> GetTokenSwapConfigAsync(long targetChainId, long sourceChainId,
        string receiver, string tokenAddress, string symbol)
    {
        try
        {
            var indexerResult = await GraphQLHelper.SendQueryAsync<IndexerTokenSwapConfigInfo>(GetClient(), new()
            {
                Query =
                    @"query($targetChainId:Long!,$sourceChainId:Long!,$receiver:String!,$tokenAddress:String,$symbol:String){
                    tokenSwapConfig(input: {targetChainId:$targetChainId,sourceChainId:$sourceChainId,receiver:$receiver,tokenAddress:$tokenAddress,symbol:$symbol}){
                            targetChainId,
                            sourceChainId,
                            receiver,
                            tokenAddress,
                            symbol,
                            extraData
                    }
                }",
                Variables = new
                {
                    targetChainId = targetChainId,
                    sourceChainId = sourceChainId,
                    receiver = receiver,
                    tokenAddress = tokenAddress,
                    symbol = symbol
                }
            });
            return indexerResult ?? new();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] Get TokenSwap config failed.");
            throw;
        }
    }

    private IGraphQLClient GetClient() => new GraphQLHttpClient(
        new GraphQLHttpClientOptions { EndPoint = new Uri(_options.BaseUrl + _options.GraphQlUri) },
        new NewtonsoftJsonSerializer());
}