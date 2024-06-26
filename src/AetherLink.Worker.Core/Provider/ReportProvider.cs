using System.Threading.Tasks;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IReportProvider
{
    public Task SetAsync(ReportDto report);
    public Task<ReportDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase;
}

public class ReportProvider : IReportProvider, ITransientDependency
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<ReportProvider> _logger;

    public ReportProvider(IStorageProvider storageProvider, ILogger<ReportProvider> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
    }

    public async Task SetAsync(ReportDto report)
    {
        var key = IdGeneratorHelper.GenerateReportRedisId(report.ChainId, report.RequestId, report.Epoch);

        _logger.LogDebug("[ReportProvider] Start to set request {key}. result:{state}", key, report.Observations);

        await _storageProvider.SetAsync(key, report);
    }

    public async Task<ReportDto> GetAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _storageProvider.GetAsync<ReportDto>(
            IdGeneratorHelper.GenerateReportRedisId(arg.ChainId, arg.RequestId, arg.Epoch));
}