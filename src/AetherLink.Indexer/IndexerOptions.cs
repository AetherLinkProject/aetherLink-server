using System.Collections.Generic;
using AetherLink.Indexer.Dtos;

namespace AetherLink.Indexer;

public class AeFinderOptions
{
    public string BaseUrl { get; set; }
    public string GraphQlUri { get; set; } = "/api/app/graphql/aetherlink";
    public string SyncStateUri { get; set; } = "/api/apps/sync-state/aetherlink";
}

public class TonIndexerOption
{
    public string ContractAddress { get; set; }
    public string Skip { get; set; } = "0";
    public string Url { get; set; }
    public string ApiKey { get; set; }
}