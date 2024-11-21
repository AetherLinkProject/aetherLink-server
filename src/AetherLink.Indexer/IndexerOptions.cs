namespace AetherLink.Indexer;

public class AeFinderOptions
{
    public string BaseUrl { get; set; }
    public string GraphQlUri { get; set; } = "/api/app/graphql/aetherlink";
    public string SyncStateUri { get; set; } = "/api/apps/sync-state/aetherlink";
}