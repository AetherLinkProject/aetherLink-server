public interface IChainBalanceProvider
{
    string ChainType { get; }
    Task<decimal> GetBalanceAsync(string address);
}

public abstract class ChainBalanceProvider : IChainBalanceProvider
{
    public abstract string ChainType { get; }
    public abstract Task<decimal> GetBalanceAsync(string address);
}