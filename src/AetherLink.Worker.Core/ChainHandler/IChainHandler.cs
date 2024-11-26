using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.ChainHandler;

public interface IChainReader
{
    long ChainId { get; }
    Task<byte[]> CallTransactionAsync(byte[] transaction);
    Task<TransactionResultDto> GetTransactionResultAsync(string transactionId);
    string ConvertBytesToAddressStr(byte[] addressBytes);
}

public interface IChainWriter
{
    long ChainId { get; }

    Task<string> SendCommitTransactionAsync(ReportContextDto reportContext, Dictionary<int, byte[]> signatures,
        CrossChainDataDto crossChainDat);
}

public abstract class ChainReader : IChainReader
{
    public abstract long ChainId { get; }
    public abstract Task<byte[]> CallTransactionAsync(byte[] transaction);
    public abstract Task<TransactionResultDto> GetTransactionResultAsync(string transactionId);
    public abstract string ConvertBytesToAddressStr(byte[] addressBytes);
}

public abstract class ChainWriter : IChainWriter
{
    public abstract long ChainId { get; }

    public abstract Task<string> SendCommitTransactionAsync(ReportContextDto reportContext,
        Dictionary<int, byte[]> signatures, CrossChainDataDto crossChainDat);
}

public class TransactionResultDto
{
    public TransactionState State { get; set; }
}

public enum TransactionState
{
    NotExist,
    Success,
    Pending,
    Fail
}