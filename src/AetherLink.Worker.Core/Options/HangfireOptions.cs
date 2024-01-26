using Hangfire.Dashboard;

namespace AetherLink.Worker.Core.Options;

public class HangfireOptions
{
    public HangfireRedisStorage RedisStorage { get; set; }
    public int WorkerCount { get; set; }
    public bool UseDashboard { get; set; } = false;
}

public class HangfireRedisStorage
{
    public string Host { get; set; }
    public string Prefix { get; set; }
    public int DbIndex { get; set; }
}

public class CustomAuthorizeFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}