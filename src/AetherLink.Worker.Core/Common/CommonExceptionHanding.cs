using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;

namespace AetherLink.Worker.Core.Common;

public static class CommonExceptionHanding
{
    public static async Task<FlowBehavior> ContinueWithCommonMessage(Exception ex, string message)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = true
        };
    }
    
    public static async Task<FlowBehavior> ThrowWithCommonMessage(Exception ex, string message)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = null
        };
    }
}