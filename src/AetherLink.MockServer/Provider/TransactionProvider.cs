using System.Collections.Concurrent;
using AElf;
using AElf.Client.Dto;
using AElf.ExceptionHandler;
using AElf.Types;
using AetherLink.Contracts.Oracle;
using AetherLink.MockServer.Common;
using AetherLink.MockServer.GraphQL.Dtos;
using Google.Protobuf;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Asn1.X509;
using BlockHelper = AetherLink.MockServer.Common.BlockHelper;

namespace AetherLink.MockServer.Provider;

public interface ITransactionProvider
{
    Task<TransactionResultDto> GetTransactionResultAsync(string txId);
    Task<string> GenerateTransactionIdAsync(string rawTransaction);
    Task<string> CreateTransactionAsync(string chainId, string name);
    Task<List<OcrJobEventDto>> GetJobEventsByBlockHeightAsync(long from, long to);
}

public class TransactionProvider : ITransactionProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, Transaction> _txDict = new();
    private readonly List<TransactionWithHeight> _txs = new();

    public async Task<TransactionResultDto> GetTransactionResultAsync(string txId)
    {
        return new TransactionResultDto
        {
            TransactionId = txId,
            Status = "MINED",
            Logs = new[]
            {
                new LogEventDto
                {
                    Address = null,
                    Name = null,
                    Indexed = new string[] { },
                    NonIndexed = null
                }
            },
            Bloom = "Bloom",
            BlockNumber = new Random().Next(),
            BlockHash = "BlockHash",
            Transaction = new()
            {
                From = _txDict[txId].From.ToString(),
                To = _txDict[txId].To.ToString(),
                RefBlockNumber = _txDict[txId].RefBlockNumber,
                RefBlockPrefix = _txDict[txId].RefBlockPrefix.ToString(),
                MethodName = _txDict[txId].MethodName,
                Params = _txDict[txId].Params.ToString(),
                Signature = _txDict[txId].Signature.ToString()
            },
            ReturnValue = "",
            Error = null
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TransactionProvider),
        MethodName = nameof(HandleException))]
    public virtual async Task<string> GenerateTransactionIdAsync(string rawTransaction)
    {
        var byteArray = ByteArrayHelper.HexStringToByteArray(rawTransaction);
        var transaction = Transaction.Parser.ParseFrom(byteArray);
        var txId = transaction.GetHash().ToHex();
        _txDict[txId] = transaction;
        return txId;
    }

    public async Task<string> CreateTransactionAsync(string chainId, string name)
    {
        var transaction = new Transaction
        {
            From = Address.FromPublicKey("AAA".HexToByteArray()),
            To = Address.FromPublicKey("BBB".HexToByteArray()),
            RefBlockNumber = BlockHelper.GetMockBlockHeight() - 100,
            RefBlockPrefix = ByteString.FromBase64("e38c4fb1cf6af05878657cb3f7b5fc8a5fcfb2eec19cd76b73abb831973fbf4e"),
            Signature = ByteString.Empty
        };

        switch (name)
        {
            case "df":
                transaction.MethodName = "StartRequest";
                transaction.Params = new SendRequestInput { SubscriptionId = 1, RequestTypeIndex = 1 }.ToByteString();
                break;
            case "cancel":
                transaction.MethodName = "CancelRequest";
                transaction.Params = new CancelRequestInput
                {
                    RequestId = Hash.Empty,
                    SubscriptionId = 1,
                    Consumer = Address.FromPublicKey("CCC".HexToByteArray()),
                    RequestTypeIndex = 1
                }.ToByteString();
                break;
            case "google":
                transaction.MethodName = "StartRequest";
                transaction.Params = new SendRequestInput { SubscriptionId = 1, RequestTypeIndex = 1 }.ToByteString();
                break;
            default: throw new UserFriendlyException("not implemented");
        }

        _txs.Add(new() { ChainId = chainId, Height = BlockHelper.GetMockBlockHeight() + 3000, T = transaction });

        return transaction.GetHash().ToString();
    }

    public async Task<List<OcrJobEventDto>> GetJobEventsByBlockHeightAsync(long from, long to)
    {
        // new()
        // {
        //     TransactionId = context.TransactionId,
        //     RequestId = eventValue.RequestId.ToHex(),
        //     RequestTypeIndex = eventValue.RequestTypeIndex,
        //     StartTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
        //     Commitment = eventValue.Commitment.ToBase64()
        // }

        return _txs.Where(t => t.Height > from && t.Height < to).Select(t =>
            new OcrJobEventDto
            {
                ChainId = t.ChainId,
                RequestId = "requestId",
                BlockHeight = t.Height,
                BlockHash = "",
                RequestTypeIndex = 0,
                TransactionId = t.T.GetHash().ToString(),
                StartTime = t.Height,
                Commitment = "commitment"
            }).ToList();
    }

    #region Exception handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = HashHelper.ComputeFrom(DateTime.UtcNow.Microsecond).ToHex()
        };
    }

    #endregion
}

public class TransactionWithHeight
{
    public string ChainId { get; set; }
    public long Height { get; set; }
    public Transaction T { get; set; }
}