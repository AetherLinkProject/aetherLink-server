using Volo.Abp.AspNetCore.Mvc;

namespace AetherlinkPriceServer.Controller;

public abstract class AetherlinkPriceServerController : AbpControllerBase
{
    protected AetherlinkPriceServerController()
    {
        LocalizationResource = typeof(AetherlinkPriceServerResource);
    }
}