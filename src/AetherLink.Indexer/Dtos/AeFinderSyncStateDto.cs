using System.Collections.Generic;

namespace AetherLink.Indexer.Dtos;

public class AeFinderSyncStateDto
{
    public CurrentVersionDto CurrentVersion { get; set; }
}

public class CurrentVersionDto
{
    public List<ChainItemDto> Items { get; set; }
}

public class ChainItemDto
{
    public string ChainId { get; set; }
    public long BestChainHeight { get; set; }
    public long LastIrreversibleBlockHeight { get; set; }
}