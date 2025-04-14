using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class TonPublicOptions
{
    public string ContractAddress { get; set; }
    public string SkipTransactionLt { get; set; } = "0";
    public List<OracleNodeInfo> OracleNodeInfoList { get; set; }
}

public class OracleNodeInfo
{
    public int Index { get; set; }
    public string PublicKey { get; set; }
}

public class TonPrivateOptions
{
    public string TransmitterSecretKey { get; set; }
    public string TransmitterFee { get; set; } = "0.05";
}

public class TonChainStatesOptions
{
    public int StatesCheckInterval { get; set; } = 5000;
}