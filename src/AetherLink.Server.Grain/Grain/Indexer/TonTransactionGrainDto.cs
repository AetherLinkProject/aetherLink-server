namespace AetherLink.Server.Grains.Grain.Indexer;

[GenerateSerializer]
public class TonTransactionGrainDto
{
    [Id(0)] public string Hash { get; set; }
    [Id(1)] public long Lt { get; set; }
    [Id(2)] public string TraceId { get; set; }
    [Id(3)] public TonInMessageGrainDto InMsg { get; set; }
    [Id(4)] public List<TonInMessageGrainDto> OutMsgs { get; set; }
    [Id(5)] public long StartTime { get; set; }
    [Id(6)] public long Now { get; set; }
}

[GenerateSerializer]
public class TonInMessageGrainDto
{
    [Id(0)] public string Opcode { get; set; }
    [Id(1)] public TonMessageContentGrainDto MessageContent { get; set; }
}

[GenerateSerializer]
public class TonMessageContentGrainDto
{
    [Id(0)] public string Body { get; set; }
}