namespace AetherLink.Server.Grains.State;

[GenerateSerializer]
public class TransactionIdState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string GrainId { get; set; }
}