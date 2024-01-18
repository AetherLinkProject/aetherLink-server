using AetherLink.MockServer.GraphQL.Dtos;
using AetherLink.MockServer.GraphQL.Input;
using GraphQL;

namespace AetherLink.MockServer.GraphQL;

public class Query
{
    private static long latestRound = 4;

    [Name("syncState")]
    public static SyncStateDto SyncState([FromServices] OcrJobProvider provider, SyncStateInput input)
    {
        return new SyncStateDto()
        {
            ConfirmedBlockHeight = provider.GetLastHeight()
        };
    }

    [Name("ocrJobEvents")]
    public static List<OcrJobEventDto> OcrJobEventsQueryAsync(OcrLogEventInput input)
    {
        switch (input.ToBlockHeight)
        {
            default:
                return new List<OcrJobEventDto> { };
        }
    }

    [Name("commitments")]
    public static List<CommitmentDto> CommitmentsQueryAsync(CommitmentsInput input)
    {
        return new List<CommitmentDto>();
    }


    [Name("latestRounds")]
    public static List<LatestRoundDto> LatestRoundQueryAsync(LatestRoundInput input)
    {
        return new List<LatestRoundDto>();
    }


    [Name("configSets")]
    public static List<ConfigDigestDto> ConfigDigestQueryAsync(ConfigDigestInput input)
    {
        return new List<ConfigDigestDto>();
    }

    [Name("requests")]
    public static List<OcrJobEventDto> RequestsQueryAsync(RequestInput input)
    {
        return new List<OcrJobEventDto>();
    }
}