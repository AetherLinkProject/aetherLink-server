using System;

namespace AetherLink.Worker.Core.Constants;

public class TonEnvConstants
{
    public const Int64 ResendMaxWaitSeconds = 30;
}

public class TonOpCodeConstants
{
    public const int ForwardTx = 3;

    public const int ReceiveTx = 4;
    
    public const int ResendTx =  9;
}

public class TonResendTypeConstants
{
    public const int IntervalSeconds = 0x01;
}