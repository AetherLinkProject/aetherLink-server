namespace AetherLink.Worker.Core.Constants;

public class EvmTransactionConstants
{
    public const string TransmitMethodName = "transmit";
    public const string ContractFilePath = "ContractBuild";
    public const string AbiFileName = "RampAbi.json";
    public const string AbiAliasName = "abi";
}

public class EvmSubscribeConstants
{
    public const int SubscribeBlockStep = 1000;
    public const int SubscribeBlockSaveStep = 10;
}