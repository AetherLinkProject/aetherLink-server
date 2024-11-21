using AetherLink.Indexer.Dtos;

namespace AetherLink.Server.Grains.State;

public class AELFBlockHeightState
{
    public List<ChainItemDto> ChainItems { get; set; }
}