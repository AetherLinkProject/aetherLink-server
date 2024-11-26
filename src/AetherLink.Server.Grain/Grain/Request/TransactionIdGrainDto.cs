namespace AetherLink.Server.Grains.Grain.Request;

[GenerateSerializer]
public class TransactionIdGrainDto
{
    [Id(0)] public string GrainId { get; set; }
}