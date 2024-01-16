namespace AetherLink.Worker.Core.Common.ContractHandler;

public interface IBlockchainClientFactory<T>
    where T : class
{
    T GetClient(string chainName);
}