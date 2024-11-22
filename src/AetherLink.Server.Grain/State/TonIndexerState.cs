namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class TonIndexerState
{
    [Id(0)] public string Id { get; set; }

    [Id(1)] public string LatestTransactionLt { get; set; }
}