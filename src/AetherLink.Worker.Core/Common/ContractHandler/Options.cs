using System.Collections.Generic;

namespace AetherLink.Worker.Core.Common.ContractHandler;

// TODO move this option to options file
public class ContractOptions
{
    public Dictionary<string, ChainInfo> ChainInfos { get; set; }
}

public class ChainInfo
{
    public string BaseUrl { get; set; }
    public string OracleContractAddress { get; set; }
    public string ConsensusContractAddress { get; set; }
    public string RampContractAddress { get; set; }
}