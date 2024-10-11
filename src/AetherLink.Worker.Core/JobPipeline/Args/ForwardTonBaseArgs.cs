using System;
using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class ForwardTonBaseArgs:JobPipelineArgsBase
{ 
    public string MessageId { get; set; }
}

public class ResendTonBaseArgs : ForwardTonBaseArgs
{
    public Int64 TargetBlockHeight { get; set; }
    
    public string TargetTxHash { get; set; }
    
    public Int64 TargetBlockGeneratorTime { get; set; }
    
    public Int64 ResendTime { get; set; }
    
    public ResendStatus Status { get; set; }

    public bool CheckNeedResend()
    {
        if(Status == ResendStatus.ChainConfirm)
        {
            return false;
        }
        
        var dtNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        if (dtNow >= ResendTime)
        {
            return true;
        }

        return false;
    }

    public bool IfCheckCommitStatus()
    {
        if (Status == ResendStatus.ChainConfirm)
        {
            return false;
        }
        
        var dtNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        if (dtNow >= CheckCommitTime)
        {
            return true;
        }

        return false;
    }
}