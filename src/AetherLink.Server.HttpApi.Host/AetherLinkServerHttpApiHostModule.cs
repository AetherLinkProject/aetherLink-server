using System;
using System.Linq;
using AetherLink.Server.Grains;
using AetherLink.Server.HttpApi;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Worker;
using AethLink.Server.MongoDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;

namespace AetherLink.Server;

[DependsOn(
    typeof(AetherLinkServerHttpApiModule),
    // typeof(AetherLinkServerMongoDbModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAutofacModule)
)]
public class AetherLinkServerHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        ConfigureConventionalControllers();
        ConfigureLocalization();
        ConfigureCors(context, configuration);
        ConfigureSwaggerServices(context, configuration);
        Configure<LogEventSearchOptions>(configuration.GetSection("Search"));
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(AetherLinkServerHttpApiModule).Assembly);
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.RemovePostFix("/"))
                            .ToArray()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();

        app.UseAbpRequestLocalization();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support APP API"); });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();

        ConfigureWorker(context);
    }

    // private static void ConfigureOrleans(ServiceConfigurationContext context, IConfiguration configuration)
    // {
    //     context.Services.AddSingleton<IClusterClient>(o =>
    //     {
    //         return new ClientBuilder()
    //             .ConfigureDefaults()
    //             .UseMongoDBClient(configuration["Orleans:MongoDBClient"])
    //             .UseMongoDBClustering(options =>
    //             {
    //                 options.DatabaseName = configuration["Orleans:DataBase"];
    //                 options.Strategy = MongoDBMembershipStrategy.SingleDocument;
    //             })
    //             .Configure<ClusterOptions>(options =>
    //             {
    //                 options.ClusterId = configuration["Orleans:ClusterId"];
    //                 options.ServiceId = configuration["Orleans:ServiceId"];
    //             })
    //             .Configure<ClientMessagingOptions>(options =>
    //             {
    //                 //the default timeout before a request is assumed to have failed.
    //                 options.ResponseTimeout =
    //                     TimeSpan.FromSeconds(ConfigurationHelper.GetValue("Orleans:ResponseTimeout",
    //                         MessagingOptions.DEFAULT_RESPONSE_TIMEOUT.Seconds));
    //             })
    //             .ConfigureApplicationParts(parts =>
    //                 parts.AddApplicationPart(typeof(AetherLinkServerGrainsModule).Assembly).WithReferences())
    //             .ConfigureLogging(builder => builder.AddProvider(o.GetService<ILoggerProvider>()))
    //             .AddNightingaleMethodFilter(o)
    //             .Build();
    //     });
    // }


    private void ConfigureWorker(ApplicationInitializationContext context)
    {
        var backgroundWorkerManger = context.ServiceProvider.GetRequiredService<IBackgroundWorkerManager>();
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<AELFLogEventSearchWorker>());
        backgroundWorkerManger.AddAsync(context.ServiceProvider.GetService<TonLogEventSearchWorker>());
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
            options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
            options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
            options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
            options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi"));
            options.Languages.Add(new LanguageInfo("is", "is", "Icelandic"));
            options.Languages.Add(new LanguageInfo("it", "it", "Italiano"));
            options.Languages.Add(new LanguageInfo("ro-RO", "ro-RO", "Română"));
            options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
            options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
            options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
            options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
            options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
            options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
            options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch"));
            options.Languages.Add(new LanguageInfo("es", "es", "Español"));
            options.Languages.Add(new LanguageInfo("el", "el", "Ελληνικά"));
        });
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "SignServer API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Scheme = "bearer",
                    Description = "Specify the authorization token.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new string[] { }
                    }
                });
            }
        );
    }
}