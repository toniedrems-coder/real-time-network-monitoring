using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

public sealed class MonitoringDbContextFactory
    : IDesignTimeDbContextFactory<MonitoringDbContext>
{
    public MonitoringDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<MonitoringDbContext>();

        var connectionString =
            "Host=127.0.0.1;Port=5434;Database=network_monitor;Username=network_monitor;Password=network_monitor_dev";

        optionsBuilder.UseNpgsql(connectionString);

        return new MonitoringDbContext(optionsBuilder.Options);
    }
}