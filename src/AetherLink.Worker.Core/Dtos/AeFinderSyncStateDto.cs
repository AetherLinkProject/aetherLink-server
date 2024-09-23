using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class AeFinderSyncStateDto
{
    public CurrentVersionDto CurrentVersion { get; set; }
}

public class CurrentVersionDto
{
    public string Version { get; set; }
    public List<ChainItemDto> Items { get; set; }
}

public class ChainItemDto
{
    public string ChainId { get; set; }
    public string LongestChainBlockHash { get; set; }
    public long LongestChainHeight { get; set; }
    public string BestChainBlockHash { get; set; }
    public long BestChainHeight { get; set; }
    public string LastIrreversibleBlockHash { get; set; }
    public long LastIrreversibleBlockHeight { get; set; }
}