using System.Collections.Generic;

namespace AetherLink.Worker.Core.Options;

public class NetworkOptions
{
    public int ListenPort { get; set; }
    public int Index { get; set; }
    public List<string> Domains { get; set; }
}