using AetherLink.Server.HttpApi.Dtos;

namespace AetherLink.Server.HttpApi.Options;

public class LogEventSearchOptions
{
    public Dictionary<string, IndexerOption> Indexers { get; set; }
    public IndexerOption GetSourceOption(ChainType sourceType) => Indexers.GetValueOrDefault(sourceType.ToString());
}

public class IndexerOption
{
    public string Name { get; set; }
    public int Interval { get; set; } = 10000;
}