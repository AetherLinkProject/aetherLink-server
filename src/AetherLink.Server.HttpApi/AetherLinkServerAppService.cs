using Volo.Abp.Application.Services;

namespace AetherLink.Server.HttpApi;

/* Inherit your application services from this class.
 */
public abstract class AetherLinkServerAppService : ApplicationService
{
    protected AetherLinkServerAppService()
    {
        LocalizationResource = typeof(AetherLinkServerResource);
    }
}