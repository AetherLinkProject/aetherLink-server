using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Provider.TonIndexer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit.Sdk;

namespace Aetherlink.Worker.Core.Tests.TonApiProvider;

public class TonIndexerRouterTests : AetherlinkTestBase<AetherlinkWorkerCoreTestModule>
{
    private readonly IOptionsSnapshot<TonPublicOptions> _tonPublicOptions;
    private readonly IEnumerable<ITonIndexerProvider> _tonIndexers;
    private readonly ILogger<TonIndexerRouter> _logger;
    private readonly int _chainStackWeight = 80;
    private readonly int _getBlockWeight = 60;
    private readonly int _tonApiWeight = 50;
    private readonly int _tonCenterWeight = 30;
    private readonly TonIndexerDto _tonIndexerDto = new TonIndexerDto();

    public TonIndexerRouterTests()
    {
        _tonPublicOptions = Substitute.For<IOptionsSnapshot<TonPublicOptions>>();
        _tonPublicOptions.Value.Returns(new TonPublicOptions
        {
            IndexerProvider = new List<string>() { "GetBlock", "TonApi", "ChainStack" },
            CommitProvider = new List<string>() { "TonCenter" }
        });

        _tonIndexers = Substitute.For<IEnumerable<ITonIndexerProvider>>();
        _tonIndexers.GetEnumerator().Returns((a) => new List<ITonIndexerProvider>()
        {
            CreateChainStackClient(),
            CreateGetBlockClient(),
            CreateTonCenterClient(),
            CreateTonApiClient(),
        }.GetEnumerator());
        
        _logger = Substitute.For<ILogger<TonIndexerRouter>>();
    }

    [Fact]
    public async Task Ton_Api_Provider_Change_Test()
    {
        var tonRouter = new TonIndexerRouter(_tonPublicOptions, _tonIndexers, _logger);
        var (txDto, indexerDto) = await tonRouter.GetSubsequentTransaction(_tonIndexerDto);
        txDto.ShouldNotBeNull();
    }

    private ChainStackClient CreateChainStackClient()
    {
        var snapshotConfig = Substitute.For<IOptionsSnapshot<ChainStackApiConfig>>();
        snapshotConfig.Value.Returns(new ChainStackApiConfig());
        var clientFactory = Substitute.For<IHttpClientFactory>();
        var storageProvider = Substitute.For<IStorageProvider>();
        var logger = Substitute.For<ILogger<ChainStackClient>>();
        var result = Substitute.For<ChainStackClient>(snapshotConfig, _tonPublicOptions, clientFactory, storageProvider,
            logger);

        result.GetSubsequentTransaction(_tonIndexerDto).Returns((indexerDto) =>
            {
                throw new HttpRequestException("Http Error");
                return (null, null);
            }
        );

        result.TryGetRequestAccess().Returns(true);

        return result;
    }

    private GetBlockClient CreateGetBlockClient()
    {
        var snapshotConfig = Substitute.For<IOptionsSnapshot<TonGetBlockProviderOptions>>();
        snapshotConfig.Value.Returns(new TonGetBlockProviderOptions());
        var clientFactory = Substitute.For<IHttpClientFactory>();
        var storageProvider = Substitute.For<IStorageProvider>();
        var logger = Substitute.For<ILogger<GetBlockClient>>();

        var result =
            Substitute.For<GetBlockClient>(snapshotConfig, _tonPublicOptions, clientFactory, storageProvider, logger);
        result.GetSubsequentTransaction(_tonIndexerDto)
            .Returns((indexerDto) => (GetCrossChainToTon(), new TonIndexerDto()));
        
        result.TryGetRequestAccess().Returns(false);

        return result;
    }

    private TonCenterClient CreateTonCenterClient()
    {
        var snapshotConfig = Substitute.For<IOptionsSnapshot<TonCenterProviderApiConfig>>();
        snapshotConfig.Value.Returns(new TonCenterProviderApiConfig());
        var clientFactory = Substitute.For<IHttpClientFactory>();
        var logger = Substitute.For<ILogger<TonCenterClient>>();

        var result = Substitute.For<TonCenterClient>(snapshotConfig, _tonPublicOptions, clientFactory, logger);
        result.GetSubsequentTransaction(_tonIndexerDto).Returns((indexerDto) =>
            (GetCrossChainToTon(), new TonIndexerDto())
        );
        
        result.TryGetRequestAccess().Returns(false);

        return result;
    }

    private TonApiClient CreateTonApiClient()
    {
        var snapshotConfig = Substitute.For<IOptionsSnapshot<TonapiProviderApiConfig>>();
        snapshotConfig.Value.Returns(new TonapiProviderApiConfig());
        var clientFactory = Substitute.For<IHttpClientFactory>();
        var logger = Substitute.For<ILogger<TonApiClient>>();

        var result = Substitute.For<TonApiClient>(snapshotConfig, _tonPublicOptions, clientFactory, logger);
        result.GetSubsequentTransaction(_tonIndexerDto).Returns((indexerDto) =>
            (GetCrossChainToTon(), new TonIndexerDto()));


        result.TryGetRequestAccess().Returns(true);
        return result;
    }

    private List<CrossChainToTonTransactionDto> GetCrossChainToTon()
    {
        return new List<CrossChainToTonTransactionDto>()
        {
            new CrossChainToTonTransactionDto()
            {
                WorkChain = 0,
                Shard = "1",
                SeqNo = 1,
                TraceId = "TraceId1",
                Hash = "Hash1",
                PrevHash = "PrevHash1",
                BlockTime = 1,
                TransactionLt = "1",
                OpCode = 3,
                OutMessage = null,
                Body = "AAAA",
                Success = true,
                ExitCode = 0,
                Aborted = false,
                Bounce = false,
                Bounced = false,
            },
            new CrossChainToTonTransactionDto()
            {
                WorkChain = 0,
                Shard = "2",
                SeqNo = 2,
                TraceId = "TraceId2",
                Hash = "Hash2",
                PrevHash = "PrevHash2",
                BlockTime = 2,
                TransactionLt = "2",
                OpCode = 3,
                OutMessage = null,
                Body = "AAAA",
                Success = true,
                ExitCode = 0,
                Aborted = false,
                Bounce = false,
                Bounced = false,
            }
        };
    }
}