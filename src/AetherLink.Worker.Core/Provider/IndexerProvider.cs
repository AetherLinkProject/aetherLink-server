using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IIndexerProvider
{
    public Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from);
    public Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from);
    public Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to, long from);
    public Task<long> GetIndexBlockHeightAsync(string chainId);
    public Task<string> GetOracleConfigAsync(string chainId);
    public Task<long> GetOracleLatestEpochAsync(string chainId, long blockHeight);
    public Task<string> GetRequestCommitmentAsync(string chainId, string requestId);
    public Task<List<TransactionEventDto>> GetTransactionLogEventsAsync(string chainId, long to, long from);
}

public class IndexerProvider : IIndexerProvider, ISingletonDependency
{
    private readonly IGraphQLHelper _graphQlHelper;
    private readonly ILogger<IndexerProvider> _logger;

    public IndexerProvider(IGraphQLHelper graphQlHelper, ILogger<IndexerProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }

    public async Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<IndexerLogEventListDto>(new GraphQLRequest
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
            _logger.LogError(e, "[Indexer] SubscribeLogs failed.");
            return new List<OcrLogEventDto>();
        }
    }


    public async Task<List<TransmittedDto>> SubscribeTransmittedAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<IndexerTransmittedListDto>(new GraphQLRequest
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
            return new List<TransmittedDto>();
        }
    }

    public async Task<List<RequestCancelledDto>> SubscribeRequestCancelledAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<IndexerRequestCancelledListDto>(new GraphQLRequest
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
            return new List<RequestCancelledDto>();
        }
    }

    public async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<ConfirmedBlockHeightRecord>(new GraphQLRequest
            {
                Query = @"
			    query($chainId:String!,$filterType:BlockFilterType!) {
                    syncState(input: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight
                }
            }",
                Variables = new
                {
                    chainId, filterType = BlockFilterType.LOG_EVENT
                }
            });

            return res.SyncState?.ConfirmedBlockHeight ?? 0;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] GetIndexBlockHeight failed.");
            throw;
        }
    }

    public async Task<string> GetOracleConfigAsync(string chainId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<OracleConfigDigestRecord>(new GraphQLRequest
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

            return res.OracleConfigDigest == null ? "" : res.OracleConfigDigest.ConfigDigest;
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
            var res = await _graphQlHelper.QueryAsync<OracleLatestEpochRecord>(new GraphQLRequest
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

            return res.OracleLatestEpoch?.Epoch ?? 0;
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
            var res = await _graphQlHelper.QueryAsync<RequestCommitmentRecord>(new GraphQLRequest
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

            return res.RequestCommitment == null ? "" : res.RequestCommitment.Commitment;
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
            var indexerResult = await _graphQlHelper.QueryAsync<IndexerTransactionEventListDto>(new GraphQLRequest
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
            return indexerResult.TransactionEvents;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeLogs failed.");
            throw;
        }
    }
}