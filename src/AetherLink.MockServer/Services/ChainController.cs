using AElf.Client.Dto;
using AetherLink.MockServer.Provider;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace AetherLink.MockServer.Services;

[RemoteService]
[Route("api/blockChain/")]
public class ChainController : AbpControllerBase
{
    private readonly ITransactionProvider _transactionProvider;

    public ChainController(ITransactionProvider transactionProvider)
    {
        _transactionProvider = transactionProvider;
    }

    [HttpPost]
    [Route("transaction")]
    public async Task<string> CreateTransactionAsync(string chainId, string method)
        => await _transactionProvider.CreateTransactionAsync(chainId, method);

    [HttpPost]
    [Route("executeTransaction")]
    public async Task<string> ExecuteTransactionAsync()
    {
        return "";
    }

    [HttpPost]
    [Route("sendTransaction")]
    public async Task<SendTransactionOutput> SendTransactionAsync(SendTransactionInput input)
        => new() { TransactionId = await _transactionProvider.GenerateTransactionIdAsync(input.RawTransaction) };

    [HttpGet]
    [Route("transactionResult")]
    public async Task<TransactionResultDto> GetTransactionResultAsync(string txId)
    {
        return await _transactionProvider.GetTransactionResultAsync(txId);
    }

    [HttpGet]
    [Route("blockHeight")]
    public async Task<long> GetBlockHeightAsync()
    {
        return new Random().Next();
    }

    [HttpGet]
    [Route("blockByHeight")]
    public async Task<BlockDto> GetBlockByHeightAsync(long blockHeight)
    {
        return new BlockDto();
    }
}