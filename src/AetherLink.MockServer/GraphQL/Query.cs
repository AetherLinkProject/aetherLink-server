using AetherLink.MockServer.Common;
using AetherLink.MockServer.GraphQL.Dtos;
using AetherLink.MockServer.GraphQL.Input;
using AetherLink.MockServer.Provider;
using GraphQL;

namespace AetherLink.MockServer.GraphQL;

public class Query
{
    [Name("syncState")]
    public static async Task<SyncStateDto> SyncState(SyncStateInput input)
    {
        var defaultTime = BlockHelper.GetMockBlockHeight();
        return input.ChainId switch
        {
            "AELF" => new() { ConfirmedBlockHeight = defaultTime + 111111 },
            _ => new() { ConfirmedBlockHeight = defaultTime }
        };
    }

    [Name("ocrJobEvents")]
    public static async Task<List<OcrJobEventDto>> JobsQueryAsync([FromServices] ITransactionProvider provider,
        OcrLogEventInput input)
    {
       return await provider.GetJobEventsByBlockHeightAsync(input.FromBlockHeight,input.ToBlockHeight);
    }

    [Name("transmitted")]
    public static async Task<List<TransmittedDto>> TransmittedQueryAsync(TransmittedInput input)
    {
        return new();
    }

    [Name("requestCancelled")]
    public static async Task<List<RequestCancelledDto>> RequestCancelledQueryAsync(RequestCancelledInput input)
    {
        return new();
    }


    [Name("requestCommitment")]
    public static async Task<CommitmentDto> RequestCommitmentQueryAsync(RequestCommitmentInput input)
    {
        return new();
    }

    [Name("oracleConfigDigest")]
    public static async Task<ConfigDigestDto> OracleConfigDigestQueryAsync(OracleConfigDigestInput input)
    {
        return new();
    }

    [Name("oracleLatestEpoch")]
    public static async Task<RequestStartEpochDto> OracleLatestEpochQueryAsync(RequestStartEpochQueryInput input)
    {
        return new();
    }
}