using System;
using System.Collections.Generic;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;

namespace AetherLink.Worker.Core.Common;

public static class IdGeneratorHelper
{
    public static string GenerateReportId(GenerateReportJobArgs args = null, JobDto job = null)
    {
        if (args != null)
            return GenerateId(MemoryConstants.ReportPrefix, args.ChainId, args.RequestId, args.Epoch,
                args.RoundId);
        if (job != null)
            return GenerateId(MemoryConstants.ReportPrefix, job.ChainId, job.RequestId, job.Epoch,
                job.RoundId);
        throw new ArgumentException("GenerateReportJobArgs and JobDto cannot be null at the same time");
    }

    public static string GenerateMultiSignatureId(GenerateMultiSignatureJobArgs args)
        => GenerateId(MemoryConstants.MultiSignaturePrefix, args.ChainId, args.RequestId, args.Epoch, args.RoundId);

    public static string GenerateJobRequestRedisId(string chainId, string requestId)
        => GenerateId(RedisKeyConstants.JobKey, chainId, requestId);

    public static string GenerateVrfJobRedisId(string chainId, string requestId)
        => GenerateId(RedisKeyConstants.VrfJobKey, chainId, requestId);

    public static string GenerateReportRedisId(string chainId, string requestId, long epoch)
        => GenerateId(RedisKeyConstants.ReportKey, chainId, requestId, epoch);

    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");
}