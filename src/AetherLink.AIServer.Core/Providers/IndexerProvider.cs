using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Helper;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.AIServer.Core.Providers;

public interface IIndexerProvider
{
    public Task<List<AIRequestDto>> SubscribeAIRequestsAsync(string chainId, long from, long to);
    public Task<List<AIReportTransmittedDto>> SubscribeAIReportTransmittedsAsync(string chainId, long to, long from);
    public Task<long> GetIndexBlockHeightAsync(string chainId);
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

    public async Task<List<AIRequestDto>> SubscribeAIRequestsAsync(string chainId, long from, long to)
    {
        try
        {
            var result = await _graphQlHelper.QueryAsync<AiRequestsListDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    aiRequests(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        chainId,
                        transactionId,
                        commitment,
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
            return result != null ? result.AiRequests : new List<AIRequestDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] Subscribe AIRequests failed.");
            return new List<AIRequestDto>();
        }
    }


    public async Task<List<AIReportTransmittedDto>> SubscribeAIReportTransmittedsAsync(string chainId, long to,
        long from)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<AiReportTransmittedAsyncListDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    aiReportTransmitted(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        chainId,
                        transactionId,
                        startTime,
                        requestId
                }
            }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult != null ? indexerResult.AiReportTransmitted : new List<AIReportTransmittedDto>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeTransmitted failed.");
            return new List<AIReportTransmittedDto>();
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
}