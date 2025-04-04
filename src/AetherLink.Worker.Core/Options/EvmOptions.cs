using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class EvmContractsOptions
{
    public Dictionary<string, EvmOptions> ContractConfig { get; set; } = new();
    public string[] DistPublicKey { get; set; }
}

public class EvmOptions
{
    public string ContractAddress { get; set; }
    public string Api { get; set; }
    public string TransmitterSecret { get; set; }
    public string SignerSecret { get; set; }
}