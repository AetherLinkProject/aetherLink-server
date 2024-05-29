using Aetherlink.PriceServer.Dtos;

namespace AetherLink.Worker.Core.Options;

public class PriceFeedsOptions
{
    public AggregateType AggregateType { get; set; } = AggregateType.Latest;
    public SourceType SourceType { get; set; } = SourceType.None;
}