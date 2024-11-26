using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Provider.TonIndexer;
using NSubstitute;
using Shouldly;

namespace Aetherlink.Worker.Core.Tests.TonApiProvider;

public class GetBlockRequestLimitTests:AetherlinkTestBase<AetherlinkWorkerCoreTestModule>
{
    private readonly IStorageProvider _storageProvider;
    private readonly string _storageKey = "GetBlockRequestedDetail";
    private RequestedDetail _getBlockRequestedDetail = new RequestedDetail();

    public GetBlockRequestLimitTests()
    {
        _storageProvider = Substitute.For<IStorageProvider>();
        _storageProvider.GetAsync<RequestedDetail>(_storageKey).Returns(_getBlockRequestedDetail);
        _storageProvider.SetAsync(_storageKey, _getBlockRequestedDetail);
    }

    [Fact]
    public async Task GetBlock_Request_Day_Limit()
    {
       var getBlockRequestLimit = new RequestLimit(1, 5, 10, _storageProvider);
        for (var i = 0; i < 5; i++)
        {
            (await getBlockRequestLimit.TryApplyAccess()).ShouldBeTrue();
            (await getBlockRequestLimit.TryApplyAccess()).ShouldBeFalse();
            await Task.Delay(1 * 1000);
        }
        
        // day limited
        (await getBlockRequestLimit.TryApplyAccess()).ShouldBeFalse();
    }
    
    [Fact]
    public async Task GetBlock_Request_Month_Limit()
    {
        var getBlockRequestLimit = new RequestLimit(1, 10, 5, _storageProvider);

        for (var i = 0; i < 5; i++)
        {
            (await getBlockRequestLimit.TryApplyAccess()).ShouldBeTrue();
            (await getBlockRequestLimit.TryApplyAccess()).ShouldBeFalse();
            await Task.Delay(1 * 1000);
        }
        
        // month limited
        (await getBlockRequestLimit.TryApplyAccess()).ShouldBeFalse();
    }
}