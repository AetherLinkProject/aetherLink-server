using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class EvmContractsOptions
{
    public Dictionary<string, EvmOptions> ContractConfig { get; set; } = new();
    public string[] OracleNodeAddressList { get; set; }
}

public class EvmOptions
{
    public string ContractAddress { get; set; }
    public string Api { get; set; }
    public string TransmitterSecret { get; set; }
    public string SignerSecret { get; set; }
    public string NetworkName { get; set; }
    public string TransmitTypeHash { get; set; }
    public string DomainSeparator { get; set; }
    public int SubscribeBlocksDelay { get; set; } = 100;
    public float MinGasPrice { get; set; } = 1;
}