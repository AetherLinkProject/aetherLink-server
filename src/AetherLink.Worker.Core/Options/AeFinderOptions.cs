namespace AetherLink.Worker.Core.Options;

public class AeFinderOptions
{
    // MainNet => https://indexer-api.aefinder.io | TestNet => https://gcptest-indexer-api.aefinder.io
    public string BaseUrl { get; set; } = "https://indexer-api.aefinder.io";
    public string GraphQlUri { get; set; } = "/api/app/graphql/aetherlink";
    public string SyncStateUri { get; set; } = "/api/apps/sync-state/aetherlink";
}