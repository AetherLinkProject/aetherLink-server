using System.Collections.Generic;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TonSdk.Core.Boc;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public interface ITonIndexer
{
    int ApiWeight { get; }

    Task<CrossChainToTonTransactionDto> GetTransactionInfo(string txId);

    Task<TransactionAnalysisDto<CrossChainToTonTransactionDto, TonIndexerDto>> GetSubsequentTransaction(TonIndexerDto tonIndexerDto);

    Task<string> GetBlockInfo();

    Task<bool> CheckAvailable();
    
    Task<bool> TryGetRequestAccess();
}