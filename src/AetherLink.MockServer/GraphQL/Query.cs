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
            case 10:
                return new List<OcrJobEventDto>()
                {
                    new()
                    {
                        ChainId = "AELF",
                        RequestId = "G2aQefM9QGRLlpKoRqy9jDpbYJQqlJaow2uzi6CNWPw=",
                        RequestTypeIndex = 2,
                        TransactionId = "8217fd00c819ed5e92632f6aa99c3cda7266a8df1f31bc259d62decbadaf279c",
                        StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                        Epoch = 3
                    }

                    // new()
                    // {
                    //     ChainId = "AELF",
                    //     RequestId = "rcbdeAVQxI6pZyKwP4GyCo/soSrYYcfD6CftpqHqbP8=",
                    //     RequestTypeIndex = 1,
                    //     TransactionId = "0c0e0fa42ebcd2a79cea3965b2974cb7cf8e3388d46c8e15dc390c1a0a04e4c7",
                    //     StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                    //     Epoch = 4
                    // }
                };
            case 20:
                return new List<OcrJobEventDto>()
                {
                    new()
                    {
                        ChainId = "AELF",
                        RequestId = "G2aQefM9QGRLlpKoRqy9jDpbYJQqlJaow2uzi6CNWPw=",
                        RequestTypeIndex = -1,
                        TransactionId = "c5f862f7c395feac1449ed8c16e18e5a492faa46e568ec7b392b0728b054fbe8",
                        StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                        Epoch = latestRound + 1
                    }
                };
            case 30:
                return new List<OcrJobEventDto>()
                {
                    new()
                    {
                        ChainId = "AELF",
                        RequestId = "G2aQefM9QGRLlpKoRqy9jDpbYJQqlJaow2uzi6CNWPw=",
                        RequestTypeIndex = 2,
                        TransactionId = "8217fd00c819ed5e92632f6aa99c3cda7266a8df1f31bc259d62decbadaf279c",
                        StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                        Epoch = 3
                    }
                };
            case 40:
                return new List<OcrJobEventDto>()
                {
                    new()
                    {
                        ChainId = "AELF",
                        RequestId = "G2aQefM9QGRLlpKoRqy9jDpbYJQqlJaow2uzi6CNWPw=",
                        RequestTypeIndex = -1,
                        TransactionId = "f92994579793c6b95418c0faffdeb2f7266ca7bdf10c9a57df6efa777b373c26",
                        StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                        Epoch = 4
                    }
                };
            case 50:
                return new List<OcrJobEventDto>()
                {
                    new()
                    {
                        ChainId = "AELF",
                        RequestId = "rcbdeAVQxI6pZyKwP4GyCo/soSrYYcfD6CftpqHqbP8=",
                        RequestTypeIndex = -2,
                        TransactionId = "",
                        StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                        Epoch = 4
                    }
                };
            default:
                return new List<OcrJobEventDto> { };
        }
    }

    [Name("commitments")]
    public static List<CommitmentDto> CommitmentsQueryAsync(CommitmentsInput input)
    {
        return new List<CommitmentDto>
        {
            new()
            {
                ChainId = "AELF",
                RequestId = "xvA8UOuVZ7tIDGdCQV7ZMq5we6GXIeJM+9b0lE7s4N4=",
                Commitment =
                    "CiIKIMbwPFDrlWe7SAxnQkFe2TKucHuhlyHiTPvW9JRO7ODeEiIKIBvnqDuliXT/CgMKFJjY3qQr9DKSrJlsOl/NMqAZaSCxGiIKICQ1GPJBqr/eVweock15kBdhQDaIpfburnYlaF5lB8H/IAEycApueyJDcm9uIjogIjEgKiAqICogKiA/IiwgICAiRGF0YUZlZWRzSm9iU3BlYyI6IHsgICAgICJUeXBlIjogIlByaWNlRmVlZHMiLCAgICAgIkN1cnJlbmN5UGFpciI6ICJCVEMvVVNEVCIgICB9IH0=",
            }
        };
    }


    [Name("latestRounds")]
    public static List<LatestRoundDto> LatestRoundQueryAsync(LatestRoundInput input)
    {
        return new List<LatestRoundDto>
        {
            new()
            {
                OracleAddress = "OracleAddress",
                EpochAndRound = latestRound
            }
        };
    }


    [Name("configSets")]
    public static List<ConfigDigestDto> ConfigDigestQueryAsync(ConfigDigestInput input)
    {
        return new List<ConfigDigestDto>
        {
            new()
            {
                ChainId = "AELF",
                OracleAddress = "OracleAddress",
                ConfigDigest = "ConfigDigest"
            }
        };
    }

    [Name("requests")]
    public static List<OcrJobEventDto> RequestsQueryAsync(RequestInput input)
    {
        return new List<OcrJobEventDto>()
        {
            new()
            {
                ChainId = "AELF",
                RequestId = "G2aQefM9QGRLlpKoRqy9jDpbYJQqlJaow2uzi6CNWPw=",
                RequestTypeIndex = 2,
                TransactionId = "8217fd00c819ed5e92632f6aa99c3cda7266a8df1f31bc259d62decbadaf279c",
                StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Epoch = 3
            }
        };
    }
}