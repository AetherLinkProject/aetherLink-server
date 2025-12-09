using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace AetherLinkServer.EntityFrameworkCore
{
    [ConnectionStringName("Default")]
    public class AetherLinkServerDbContext : AbpMongoDbContext
    {
        
    }
}