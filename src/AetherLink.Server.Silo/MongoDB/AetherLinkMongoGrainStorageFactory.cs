using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Storage;

namespace AetherLink.Server.Silo.MongoDB;

public static class AetherLinkMongoGrainStorageFactory
{
    public static IGrainStorage Create(IServiceProvider services, string name)
    {
        var optionsMonitor = services.GetRequiredService<IOptionsMonitor<MongoDBGrainStorageOptions>>();
        return ActivatorUtilities.CreateInstance<AetherLinkMongoGrainStorage>(services, optionsMonitor.Get(name));
    }
}