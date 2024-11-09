using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface ITokenSwapper
{
    public Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount);
}

public class TokenSwapper : ITokenSwapper, ITransientDependency
{
    public async Task<TokenAmountDto> ConstructSwapId(TokenAmountDto tokenAmount)
    {
        tokenAmount.SwapId = "test_swap_id";
        return tokenAmount;
    }
}