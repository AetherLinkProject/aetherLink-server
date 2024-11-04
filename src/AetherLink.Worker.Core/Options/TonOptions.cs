using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class TonPublicConfig
{
    public string ContractAddress { get; set; }
    public int IndexerPeriod { get; set; }
    public List<OracleNodeInfo> OracleNodeInfoList { get; set; }
    public List<string> IndexerProvider { get; set; }
    public List<string> CommitProvider { get; set; }
}

public class OracleNodeInfo
{
    public int Index { get; set; }
    public string PublicKey { get; set; }
}

public class TonSecretConfigOptions
{
    public string TransmitterSecretKey { get; set; }
    public string TransmitterFee { get; set; } = "0.015";
}