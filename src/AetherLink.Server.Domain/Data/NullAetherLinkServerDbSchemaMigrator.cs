using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace AetherLinkServer.Data
{
    /* This is used if database provider does't define
     * IAetherLinkServerDbSchemaMigrator implementation.
     */
    public class NullAetherLinkServerDbSchemaMigrator : IAetherLinkServerDbSchemaMigrator, ITransientDependency
    {
        public Task MigrateAsync()
        {
            return Task.CompletedTask;
        }
    }
}