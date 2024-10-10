using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class OracleInfoOptions
{
    public int ObservationsThreshold { get; set; } = 5;
    public int PartialSignaturesThreshold { get; set; } = 4;
    public Dictionary<string, ChainConfig> ChainConfig { get; set; }
}

public class ChainConfig
{
    public int ObservationsThreshold { get; set; }
    public int PartialSignaturesThreshold { get; set; }
    public string TransmitterSecret { get; set; }
    public string SignerSecret { get; set; }
    public string VRFSecret { get; set; }
    public string[] DistPublicKey { get; set; }
}