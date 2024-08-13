namespace AetherLink.AIServer.Core.ContractHandler;

public interface IBlockchainClientFactory<T> where T : class
{
    T GetClient(string chainName);
}