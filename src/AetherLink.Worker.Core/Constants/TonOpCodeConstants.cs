using System;

namespace AetherLink.Worker.Core.Constants;

public class TonEnvConstants
{
    public const long ResendMaxWaitSeconds = 30;
    public const int PullTransactionMinWaitSecond = 3;
}

public class TonOpCodeConstants
{
    public const int ForwardTx = 0x0000003;

    public const int ReceiveTx = 0x000004;
    
    public const int ResendTx =  0x000009;
}

public class TonResendTypeConstants
{
    public const int IntervalSeconds = 0x01;
}