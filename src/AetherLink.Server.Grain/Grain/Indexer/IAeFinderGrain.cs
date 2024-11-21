namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IAeFinderGrain: IGrainWithStringKey
{
    Task UpdateConfirmBlockHeightAsync();
}