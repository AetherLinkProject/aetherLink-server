using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class ConfigDigestsDto
{
    public List<ConfigDigestDto> ConfigSets { get; set; }
}

public class ConfigDigestDto
{
    public string ChainId { get; set; }
    public string ConfigDigest { get; set; }
}