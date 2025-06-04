using System;
using System.Linq;
using AetherLink.Indexer;
using AetherLink.Metric;
using AetherLink.Server.HttpApi;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Worker.AELF;
using AetherLink.Server.HttpApi.Worker.Evm;
using AetherLink.Server.HttpApi.Worker.Ton;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Threading;
using Prometheus;

namespace AetherLinkServer;

[DependsOn(
    typeof(AetherLinkServerHttpApiModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AetherLinkIndexerModule),
    typeof(AetherLinkMetricModule),
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
        Configure<AELFOptions>(configuration.GetSection("AELF"));
        Configure<TonOptions>(configuration.GetSection("Ton"));
        Configure<BalanceMonitorOptions>(configuration.GetSection("BalanceMonitor"));
        context.Services.AddHttpClient();
        var metricsOption = configuration.GetSection("Metrics").Get<MetricsOption>();
        context.Services.AddMetricServer(options => { options.Port = metricsOption.Port; });
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
        app.UseHttpMetrics();
        app.UseConfiguredEndpoints();

        ConfigureWorker(context);
    }

    private void ConfigureWorker(ApplicationInitializationContext context)
    {
        // context.AddBackgroundWorkerAsync<ConfirmBlockHeightSearchWorker>();
        // context.AddBackgroundWorkerAsync<RequestSearchWorker>();
        // context.AddBackgroundWorkerAsync<CommitSearchWorker>();
        // context.AddBackgroundWorkerAsync<TransactionSearchWorker>();
        // context.AddBackgroundWorkerAsync<EvmSearchWorker>();
        // context.AddBackgroundWorkerAsync<EvmChainStatusSyncWorker>();
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<ConfirmBlockHeightSearchWorker>());
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<RequestSearchWorker>());
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<CommitSearchWorker>());
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<TransactionSearchWorker>());
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<EvmSearchWorker>());
        AsyncHelper.RunSync(() => context.AddBackgroundWorkerAsync<EvmChainStatusSyncWorker>());
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