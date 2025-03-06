using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class EvmContractsOptions
{
    public Dictionary<string, EvmOptions> ContractConfig { get; set; }
}

public class EvmOptions
{
    public string ContractAddress { get; set; } = "0xdaEe625927C292BB4E29b800ABeCe0Dadf10EbAb";
    public string Api { get; set; } = "https://sepolia.infura.io/v3/a22808b9b0f14e9dbb098f2b03604ce2";
    public string TransmitterSecret { get; set; }
    public string SignerSecret { get; set; }
}