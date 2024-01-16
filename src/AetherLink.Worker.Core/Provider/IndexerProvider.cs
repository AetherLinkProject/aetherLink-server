using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using GraphQL;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IIndexerProvider
{
    public Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from);
    public Task<long> GetIndexBlockHeightAsync(string chainId);
    public Task<string> GetOracleConfigAsync(string chainId);
    public Task<long> GetLatestRoundAsync(string chainId);
    public Task<string> GetCommitmentAsync(string chainId, string requestId);
    public Task<List<OcrLogEventDto>> GetJobsAsync(string chainId, string requestId, int requestType);
}

public class IndexerProvider : IIndexerProvider, ISingletonDependency
{
    private readonly IGraphQLHelper _graphQlHelper;

    public IndexerProvider(IGraphQLHelper graphQlHelper)
    {
        _graphQlHelper = graphQlHelper;
    }

    public async Task<List<OcrLogEventDto>> SubscribeLogsAsync(string chainId, long to, long from)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<IndexerLogEventListDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    ocrJobEvents(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        requestTypeIndex,
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
        return indexerResult.OcrJobEvents;
    }

    public async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<ConfirmedBlockHeightRecord>(new GraphQLRequest
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

        return indexerResult.SyncState.ConfirmedBlockHeight;
    }

    public async Task<string> GetOracleConfigAsync(string chainId)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<ConfigDigestsDto>(new GraphQLRequest
            {
                Query = @"
			        query($chainId:String!){
                        configSets(input: {chainId:$chainId}){
                            chainId,
                            configDigest
                    }
                }",
                Variables = new
                {
                    chainId = chainId
                }
            });

            return indexerResult.ConfigSets[0].ConfigDigest;
        }
        catch (Exception e)
        {
            return "";
        }
    }

    public async Task<long> GetLatestRoundAsync(string chainId)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<LatestRoundsDto>(new GraphQLRequest
            {
                Query = @"
			        query($chainId:String!){
                        latestRounds(input: {chainId:$chainId}){
                            epochAndRound
                    }
                }",
                Variables = new
                {
                    chainId = chainId
                }
            });

            return indexerResult.LatestRounds[0].EpochAndRound;
        }
        catch (Exception e)
        {
            return 0;
        }
    }

    public async Task<string> GetCommitmentAsync(string chainId, string requestId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<CommitmentsDto>(new GraphQLRequest
            {
                Query = @"
                    query($chainId:String!,$requestId:String!){
                        commitments(input: {chainId:$chainId,requestId:$requestId}){
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

            return res.Commitments[0].Commitment;
        }
        catch (Exception e)
        {
            return "";
        }
    }

    public async Task<List<OcrLogEventDto>> GetJobsAsync(string chainId, string requestId, int requestType)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<RequestsDto>(new GraphQLRequest
            {
                Query = @"
                    query($chainId:String!,$requestId:String!,$requestType:Int!){
                        requests(input: {chainId:$chainId,requestId:$requestId,requestType:$requestType}){
                            requestTypeIndex,
                            chainId,
                            transactionId,
                            startTime,
                            epoch,
                            requestId
                    }
            }",
                Variables = new
                {
                    chainId = chainId, requestId = requestId, requestType = requestType
                }
            });
            return indexerResult.Requests;
        }
        catch (Exception e)
        {
            return new List<OcrLogEventDto>();
        }
    }
}