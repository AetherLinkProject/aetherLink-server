using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IAeFinderProvider
{
    public Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from);
    public Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from);
    public Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to, long from);
    public Task<List<ChainItemDto>> GetChainSyncStateAsync();
    public Task<string> GetOracleConfigAsync(string chainId);
    public Task<long> GetOracleLatestEpochAsync(string chainId, long blockHeight);
    public Task<string> GetRequestCommitmentAsync(string chainId, string requestId);
    public Task<List<TransactionEventDto>> GetTransactionLogEventsAsync(string chainId, long to, long from);
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

    [ExceptionHandler(typeof(Exception), Message = "[Indexer] Subscribe Logs failed.",
        ReturnDefault = ReturnDefault.New)]
    public virtual async Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from)
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

    [ExceptionHandler(typeof(Exception), Message = "[Indexer] SubscribeTransmitted failed.",
        ReturnDefault = ReturnDefault.New)]
    public virtual async Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from)
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

    [ExceptionHandler(typeof(Exception), Message = "[Indexer] SubscribeRequestCancelled failed.",
        ReturnDefault = ReturnDefault.New)]
    public virtual async Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to,
        long from)
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

    public async Task<List<ChainItemDto>> GetChainSyncStateAsync()
    {
        var result = await _httpClient.GetAsync<AeFinderSyncStateDto>(_options.BaseUrl + _options.SyncStateUri, new());
        return result.CurrentVersion.Items;
    }

    [ExceptionHandler(typeof(Exception), Message = "[Indexer] GetOracleConfig failed.",
        ReturnDefault = ReturnDefault.Default)]
    public virtual async Task<string> GetOracleConfigAsync(string chainId)
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

    [ExceptionHandler(typeof(Exception), Message = "[Indexer] GetRequestCommitment failed.",
        ReturnDefault = ReturnDefault.Default)]
    public virtual async Task<string> GetRequestCommitmentAsync(string chainId, string requestId)
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

    private IGraphQLClient GetClient() => new GraphQLHttpClient(
        new GraphQLHttpClientOptions { EndPoint = new Uri(_options.BaseUrl + _options.GraphQlUri) },
        new NewtonsoftJsonSerializer());
}