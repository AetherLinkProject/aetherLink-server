using Volo.Abp.AspNetCore.Mvc;

namespace AetherLink.Server.HttpApi.Controller;

public abstract class AetherLinkServerController : AbpControllerBase
{
    protected AetherLinkServerController()
    {
        LocalizationResource = typeof(AetherLinkServerResource);
    }
}