using TMS.Service;
using System.Text.Json;

namespace TMS.Historical
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly DBHerper _dbHelper;
        private HistoricalConfiguration _config;
        private ErrorMessage error;
        public Worker(ILogger<Worker> logger, HistoricalConfiguration configuration)
        {
            _logger = logger;
            _dbHelper = new DBHerper();
            _config = configuration;
            error = new ErrorMessage();

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                error = _dbHelper.SetHistorycalData();
                var json = JsonSerializer.Serialize(error);
                _logger.LogInformation("Worker running at: {time},status historical: {0}", DateTimeOffset.Now ,json);

                await Task.Delay(_config.TimeInterval, stoppingToken);
            }
        }
    }
}