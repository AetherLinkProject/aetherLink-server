using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Provider.TonIndexer;
using NSubstitute;
using Shouldly;

namespace Aetherlink.Worker.Core.Tests.TonApiProvider;

public class ChainStackRequestLimitTests : AetherlinkTestBase<AetherlinkWorkerCoreTestModule>
{
    private readonly IStorageProvider _storageProvider;
    private int _perSecondLimit = 1;
    private int _perMonthLimit = 5;
    private readonly ChainStackRequestLimit _chainStackRequestLimit;
    private readonly string _storageKey = "ChainStackRequestedDetail";
    private ChainStackRequestedDetail _chainStackRequestedDetail = new ChainStackRequestedDetail();

    public ChainStackRequestLimitTests()
    {
        _storageProvider = Substitute.For<IStorageProvider>();
        _chainStackRequestLimit = new ChainStackRequestLimit(_perSecondLimit, _perMonthLimit, _storageProvider);
        _storageProvider.GetAsync<ChainStackRequestedDetail>(_storageKey).Returns(_chainStackRequestedDetail);
        _storageProvider.SetAsync(_storageKey, _chainStackRequestedDetail);
    }

    [Fact]
    public async Task ChainStack_Request_Limit()
    {
        for (var i = 0; i < _perMonthLimit; i++)
        {
            (await _chainStackRequestLimit.TryApplyAccess()).ShouldBeTrue();
            (await _chainStackRequestLimit.TryApplyAccess()).ShouldBeFalse();
            await Task.Delay(1 * 1000);
        }
        
        // month limited
        (await _chainStackRequestLimit.TryApplyAccess()).ShouldBeFalse();
    }
}