namespace AetherLink.Server.HttpApi.Options;

public class EVMOptions
{
    public int TransactionSearchTimer { get; set; } = 30000;
    public int DelayTransactionSearchTimer { get; set; } = 10000;
    public int SubscribeBlocksDelay { get; set; } = 100;
}