using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class TonPublicConfigOptions
{
    public string ContractAddress { get; set; }
    
    public List<OracleNodeInfo> OracleNodeInfoList { get; set; }
}

public class OracleNodeInfo
{
    public int Index { get; set; }
    public string PublicKey { get; set; }
}

public class TonSecretConfigOptions
{
    public string TransmitterSecretKey { get; set; }

    public string TransmitterPublicKey { get; set; }
    
    public string TransmitterFee { get; set; }
}