using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

public sealed class MonitoringDbContextFactory
    : IDesignTimeDbContextFactory<MonitoringDbContext>
{
    public MonitoringDbContext CreateDbContext(string[] args)
    {
          var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

        var optionsBuilder =
            new DbContextOptionsBuilder<MonitoringDbContext>();

        var connectionString  = configuration.GetConnectionString("MonitoringDb")
         ?? throw new InvalidOperationException(
            "Connection string 'MonitoringDb' was not found.");
            
        optionsBuilder.UseNpgsql(connectionString);


        return new MonitoringDbContext(optionsBuilder.Options);
    }
}