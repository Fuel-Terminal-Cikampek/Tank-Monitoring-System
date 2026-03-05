using TMS.TankDipPosting;
using TMS.TankDipPosting.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

public class Program
{
    public static void Main(string[] args)
    {
        IHost host = CreateHostBuilder(args).Build();
        CreateDatabaseIfNotExist(host);
        host.Run();
    }
    private static void CreateDatabaseIfNotExist(IHost host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<AppDbContext>();
                context.Database.EnsureCreated();
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
    public static IHostBuilder CreateHostBuilder(string[] args) =>
                Host.CreateDefaultBuilder(args)
                    .ConfigureLogging(options => {
                        if (OperatingSystem.IsWindows())
                        {
                            options.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information);
                        }
                    })
                    .ConfigureServices((hostContext, services) => {
                        IConfiguration configuration = hostContext.Configuration;
                        //get connection SQL Server
                        AppSetting.ConnectionString = configuration.GetConnectionString("DefaultConnection");
                        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                        optionsBuilder.UseSqlServer(AppSetting.ConnectionString); 
                        services.AddScoped<AppDbContext>(db => new AppDbContext(optionsBuilder.Options));
                        //get Tank Configuration
                        TankDipConfiguration tankConfig = configuration.GetSection("TankDipConfiguration").Get<TankDipConfiguration>();
                        IntervalPosting interval = configuration.GetSection("IntervalPosting").Get<IntervalPosting>();
                        services.AddSingleton(tankConfig);
                        services.AddSingleton(interval);
                        // Register FileLogger as singleton
                        services.AddSingleton<FileLogger>();
                        services.AddHostedService<WorkerAutoTankDip>();
                        services.Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "Auto TankDip Posting";
                            config.SourceName = "Auto TankDip Posting source";
                        });
                    })
        .UseWindowsService();
}
