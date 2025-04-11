using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class MasterChainInfoDto
{
    public TonBlock Last { get; set; }
}

public class TonLatestBlockInfoDto
{
    public string Shard { get; set; }
    public long McBlockSeqno { get; set; }
    public string StartLt { get; set; }
    public string EndLt { get; set; }
}

public class TonBlock
{
    public int Workchain { get; set; }
    public string Shard { get; set; }
    public string StartLt { get; set; }
    public string EndLt { get; set; }
    public BlockId MasterchainBlockRef { get; set; }
    public List<BlockId> PrevBlocks { get; set; }
}