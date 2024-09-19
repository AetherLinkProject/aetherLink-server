using AetherLink.Contracts.Automation;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using Google.Protobuf;
using Oracle;

namespace AetherLink.Worker.Core.Automation.Providers;

public static class AutomationHelper
{
    // [Parse] RegisterUpkeepInput 
    public static TriggerType GetTriggerType(Commitment commitment)
        => RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData).TriggerType;

    public static string GetTriggerData(Commitment commitment)
        => RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData).TriggerData.ToStringUtf8();

    public static string GetUpkeepAddress(Commitment commitment)
        => RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData).UpkeepContract.ToString();

    public static ByteString GetUpkeepPerformData(Commitment commitment)
        => RegisterUpkeepInput.Parser.ParseFrom(commitment.SpecificData).PerformData;

    // [ID][log]
    public static string GenerateLogTriggerKey(string eventId, string upkeepId)
        => IdGeneratorHelper.GenerateId(RedisKeyConstants.UpkeepLogTriggerInfoKey, eventId, upkeepId);

    public static string GetLogTriggerKeyByPayload(string chainId, string upkeepId, byte[] payload)
    {
        var eventKey = GenerateTransactionEventKeyByPayload(payload);
        var upkeepKey = IdGeneratorHelper.GenerateUpkeepInfoId(chainId, upkeepId);
        return GenerateLogTriggerKey(eventKey, upkeepKey);
    }

    public static string GenerateLogTriggerId(string eventId, string upkeepId)
        => IdGeneratorHelper.GenerateId(eventId, upkeepId);

    public static string GenerateTransactionEventKeyByPayload(byte[] payload)
    {
        var checkData = LogTriggerCheckData.Parser.ParseFrom(payload);
        return GenerateTransactionEventKey(checkData.ChainId, checkData.ContractAddress, checkData.EventName,
            checkData.BlockHeight, checkData.Index);
    }

    public static string GenerateTransactionEventKey(string chainId, string contractAddress, string eventName,
        long blockHeight, int index) => IdGeneratorHelper.GenerateId(RedisKeyConstants.TransactionEventKey, chainId,
        contractAddress, eventName, blockHeight, index);

    // [ID][cron]
    public static string GenerateCronUpkeepId(OCRContext context)
        => IdGeneratorHelper.GenerateId(context.ChainId, context.RequestId, context.Epoch, context.RoundId);
}