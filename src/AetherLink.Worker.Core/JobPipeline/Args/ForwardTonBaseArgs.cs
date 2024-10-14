using System;
using AetherLink.Worker.Core.Dtos;

namespace AetherLink.Worker.Core.JobPipeline.Args;

public class ForwardTonBaseArgs:JobPipelineArgsBase
{ 
    public string MessageId { get; set; }
}

public class ResendTonBaseArgs : ForwardTonBaseArgs
{
    public long TargetBlockHeight { get; set; }
    
    public string TargetTxHash { get; set; }
    
    public long TargetBlockGeneratorTime { get; set; }
    
    public long ResendTime { get; set; }
    
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

}