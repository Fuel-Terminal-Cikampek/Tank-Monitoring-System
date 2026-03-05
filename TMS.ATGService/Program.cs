using TMS.ATGService;
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
                .ConfigureLogging(options =>
                {
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

                        // Get FD-INTERFACE Configuration
                        FDInterfaceConfiguration fdConfig = configuration.GetSection("FDInterfaceConfiguration").Get<FDInterfaceConfiguration>();
                        services.AddSingleton(fdConfig);

                        // Get Tank Configuration (for Modbus mode)
                        tankConfiguration tankConfig = configuration.GetSection("TankConfiguration").Get<tankConfiguration>();
                        services.AddSingleton(tankConfig);

                        // Choose Worker based on configuration
                        if (fdConfig != null && fdConfig.EnableXmlMode)
                        {
                            // XML Mode: Read from FD-INTERFACE system.xml
                            services.AddHostedService<WorkerXmlMode>();
                            Console.WriteLine("=== TMS.ATGService Starting in XML MODE (FD-INTERFACE Reader) ===");
                        }
                        else
                        {
                            // Modbus Mode: Direct communication to ATG devices
                            services.AddHostedService<Worker>();
                            Console.WriteLine("=== TMS.ATGService Starting in MODBUS MODE (Direct ATG) ===");
                        }

                        services.Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "ATG Service";
                            config.SourceName = "ATG Service Source";
                        });

                    }
                    )
        .UseWindowsService();
}
