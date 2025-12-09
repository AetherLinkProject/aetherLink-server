using System.Threading.Tasks;

namespace AetherLinkServer.Data
{
    public interface IAetherLinkServerDbSchemaMigrator
    {
        Task MigrateAsync();
    }
}
