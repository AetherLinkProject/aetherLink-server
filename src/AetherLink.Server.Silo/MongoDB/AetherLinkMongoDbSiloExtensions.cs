using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Runtime.Hosting;

namespace AetherLink.Server.Silo.MongoDB;

public static class AetherLinkMongoDbSiloExtensions
{
    public static ISiloBuilder AddAetherLinkMongoDBGrainStorage(this ISiloBuilder builder, string name,
        Action<MongoDBGrainStorageOptions> configureOptions)
    {
        return builder.ConfigureServices((Action<IServiceCollection>)(services =>
            services.AddAetherLinkMongoDBGrainStorage(name, configureOptions)));
    }

    public static IServiceCollection AddAetherLinkMongoDBGrainStorage(this IServiceCollection services, string name,
        Action<MongoDBGrainStorageOptions> configureOptions)
    {
        return services.AddAetherLinkMongoDBGrainStorage(name, ob => ob.Configure(configureOptions));
    }

    public static IServiceCollection AddAetherLinkMongoDBGrainStorage(this IServiceCollection services, string name,
        Action<OptionsBuilder<MongoDBGrainStorageOptions>> configureOptions = null)
    {
        configureOptions?.Invoke(services.AddOptions<MongoDBGrainStorageOptions>(name));
        services.TryAddSingleton<IGrainStateSerializer, JsonGrainStateSerializer>();
        services.AddTransient<IConfigurationValidator>(sp =>
            new MongoDBGrainStorageOptionsValidator(
                sp.GetRequiredService<IOptionsMonitor<MongoDBGrainStorageOptions>>().Get(name), name));
        services.ConfigureNamedOptionForLogging<MongoDBGrainStorageOptions>(name);
        services
            .AddTransient<IPostConfigureOptions<MongoDBGrainStorageOptions>,
                AetherLinkMongoDBGrainStorageConfigurator>();
        return services.AddGrainStorage(name, AetherLinkMongoGrainStorageFactory.Create);
    }
}