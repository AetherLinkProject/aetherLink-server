namespace AetherLink.Server.HttpApi.Constants;

public class GrainKeyConstants
{
    public const string SearchRampRequestsGrainKey = "searchRampRequests";
    public const string ConfirmBlockHeightGrainKey = "confirmBlockHeight";
    public const string SearchRequestsCommittedGrainKey = "searchRequestsCommitted";
    public const string CommitWorkerConsumedBlockHeightGrainKey = "commitWokerConsumedBlockHeight";
    public const string RequestWorkerConsumedBlockHeightGrainKey = "requestWokerConsumedBlockHeight";

    // TON
    public const string SearchTransactionGrainKey = "searchTransaction";
}