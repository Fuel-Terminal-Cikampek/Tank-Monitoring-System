using Microsoft.EntityFrameworkCore;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using TMS.ATGService;
using TMS.Models;

namespace TMS.AutoBackupDatabase
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppConfig _appConfig;
        private readonly DBHerper _dbHelper;
        private readonly IServiceScopeFactory _scopeFactory;

        private DateTime _lastBackupDate = DateTime.MinValue;

        public Worker(ILogger<Worker> logger, AppConfig appConfig, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _appConfig = appConfig;
            _scopeFactory = scopeFactory;
            _dbHelper = new DBHerper();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time} - Mode: {mode}", DateTimeOffset.Now, _appConfig.backupMode);

            if (_appConfig.backupMode == "Test")
            {
                _logger.LogInformation("=== RUNNING TEST BACKUP MODE ===");
                await RunTestBackup();
                _logger.LogInformation("=== TEST BACKUP COMPLETED - Service will continue running ===");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_appConfig.backupMode == "Monthly")
                {
                    await RunMonthlyBackup();
                }
                else if (_appConfig.backupMode == "Test")
                {
                    _logger.LogInformation("Test mode - waiting...");
                }
                else if (_appConfig.backupMode == "Daily")
                {
                    RunDailyBackup();
                }

                await Task.Delay(60000, stoppingToken);
            }
        }

        private async Task RunTestBackup()
        {
            try
            {
                DateTime now = DateTime.Now;
                DateTime lastMonth = now.AddMonths(-1);
                DateTime startDate = new DateTime(lastMonth.Year, lastMonth.Month, 17, 21, 55, 0);
                DateTime endDate = now;

                _logger.LogInformation("===========================================");
                _logger.LogInformation("TEST BACKUP - Date Range:");
                _logger.LogInformation("  Start: {start}", startDate.ToString("yyyy-MM-dd HH:mm:ss"));
                _logger.LogInformation("  End:   {end}", endDate.ToString("yyyy-MM-dd HH:mm:ss"));
                _logger.LogInformation("===========================================");

                ExportExcelTestRange(startDate, endDate);

                _logger.LogInformation("Test backup completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError("Test backup failed: {error}", ex.Message);
                _logger.LogError("Stack trace: {stack}", ex.StackTrace);
            }
        }

        private void ExportExcelTestRange(DateTime startDate, DateTime endDate)
        {
            try
            {
                var exportFolderName = $"TEST_BACKUP_{startDate:yyyyMMdd_HHmm}_to_{endDate:yyyyMMdd_HHmm}";
                var exportPath = Path.Combine(_appConfig.exportPath, exportFolderName);

                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                _logger.LogInformation("Export folder created: {path}", exportPath);

                var allTank = _dbHelper.GetAllTank();
                _logger.LogInformation("Total tanks to export: {count}", allTank.Count);

                foreach (var tank in allTank)
                {
                    try
                    {
                        if (tank == null || string.IsNullOrEmpty(tank.Tank_Name))
                        {
                            _logger.LogWarning("⚠️ Invalid tank, skipping...");
                            continue;
                        }

                        var templatePath = $"Doc/TemplateTankHistorical_{tank.Tank_Name}.xlsx";

                        if (!File.Exists(templatePath))
                        {
                            _logger.LogWarning("Template not found for tank {tank}, skipping...", tank.Tank_Name);
                            continue;
                        }

                        _logger.LogInformation("📂 Opening template: {template}", templatePath);

                        FileStream templateStream = null;
                        IWorkbook workbook = null;

                        try
                        {
                            templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
                            workbook = new XSSFWorkbook(templateStream);

                            _logger.LogInformation("Extracting data for {tank} from {start} to {end}...",
                                tank.Tank_Name, startDate.ToString("yyyy-MM-dd HH:mm:ss"), endDate.ToString("yyyy-MM-dd HH:mm:ss"));

                            var tankHistorical = _dbHelper.GetTankHistoricalByDateRange(tank.Tank_Name, startDate, endDate);

                            if (tankHistorical == null)
                            {
                                _logger.LogWarning("⚠️ GetTankHistoricalByDateRange returned NULL for {tank}", tank.Tank_Name);
                                continue;
                            }

                            _logger.LogInformation("Found {count} records for {tank}", tankHistorical.Count, tank.Tank_Name);

                            if (tankHistorical.Count > 0)
                            {
                                ISheet sheet = workbook.GetSheet(tank.Tank_Name);
                                if (sheet == null)
                                {
                                    _logger.LogError("❌ Sheet '{sheet}' not found in template {template}!",
                                        tank.Tank_Name, templatePath);
                                    continue;
                                }

                                int rowIndex = 9;
                                int number = 1;

                                Product product = null;
                                try
                                {
                                    if (!string.IsNullOrEmpty(tank.Product_ID))
                                    {
                                        product = _dbHelper.GetProductById(tank.Product_ID);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("⚠️ Failed to get product for tank {tank}: {error}", tank.Tank_Name, ex.Message);
                                }

                                string productName = product?.Product_Name ?? "Unknown Product";

                                foreach (var history in tankHistorical)
                                {
                                    try
                                    {
                                        if (history == null)
                                        {
                                            _logger.LogWarning("⚠️ NULL history record found for {tank}, skipping row...", tank.Tank_Name);
                                            continue;
                                        }

                                        IRow row = sheet.CreateRow(rowIndex);

                                        row.CreateCell(0).SetCellValue(number);
                                        row.CreateCell(1).SetCellValue(tank.Tank_Name ?? "N/A");
                                        row.CreateCell(2).SetCellValue(productName);
                                        row.CreateCell(3).SetCellValue(history.TimeStamp?.ToString("yyyy MMMM dd / HH:mm:ss") ?? "N/A");
                                        row.CreateCell(4).SetCellValue(history.Level ?? 0);
                                        row.CreateCell(5).SetCellValue(history.Level_Water ?? 0);
                                        row.CreateCell(6).SetCellValue(history.Temperature ?? 0);
                                        row.CreateCell(7).SetCellValue(history.Temperature ?? 0);
                                        row.CreateCell(8).SetCellValue(history.Density ?? 0);
                                        row.CreateCell(9).SetCellValue(history.Volume_Obs ?? 0);
                                        row.CreateCell(10).SetCellValue(history.Pumpable ?? 0);
                                        row.CreateCell(11).SetCellValue(history.Ullage ?? 0);
                                        row.CreateCell(12).SetCellValue(history.Flowrate ?? 0);

                                        rowIndex++;
                                        number++;
                                    }
                                    catch (Exception rowEx)
                                    {
                                        _logger.LogError("❌ Error writing row {row} for {tank}: {error}", number, tank.Tank_Name, rowEx.Message);
                                    }
                                }

                                IRow rowDate = sheet.CreateRow(5);
                                rowDate.CreateCell(1).SetCellValue("Date Export : ");
                                rowDate.CreateCell(2).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                                rowDate.CreateCell(3).SetCellValue($"Period: {startDate:yyyy-MM-dd HH:mm:ss} to {endDate:yyyy-MM-dd HH:mm:ss}");

                                var fileName = $"TEST_{tank.Tank_Name}_{startDate:yyyyMMdd_HHmm}_to_{endDate:yyyyMMdd_HHmm}.xlsx";
                                var fullPath = Path.Combine(exportPath, fileName);

                                using (var fs = new FileStream(fullPath, FileMode.Create))
                                {
                                    workbook.Write(fs);
                                }

                                _logger.LogInformation("✅ Exported {count} records for {tank} to {file}",
                                    tankHistorical.Count, tank.Tank_Name, fileName);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ No data found for {tank} in range {start} to {end}",
                                    tank.Tank_Name, startDate.ToString("yyyy-MM-dd HH:mm:ss"), endDate.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                        }
                        finally
                        {
                            workbook?.Close();
                            templateStream?.Close();
                            templateStream?.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("❌ Error exporting {tank}: {error}", tank.Tank_Name ?? "UNKNOWN", ex.Message);
                        _logger.LogError("Stack trace: {stack}", ex.StackTrace);
                    }
                }

                _logger.LogInformation("===========================================");
                _logger.LogInformation("Test backup completed!");
                _logger.LogInformation("Files saved to: {path}", exportPath);
                _logger.LogInformation("===========================================");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Test backup export failed: {error}", ex.Message);
                _logger.LogError("Stack trace: {stack}", ex.StackTrace);
            }
        }

        private async Task RunMonthlyBackup()
        {
            DateTime now = DateTime.Now;

            string[] arrBackupTime = _appConfig.timeBackup.Split(':');
            int backupHour = Convert.ToInt32(arrBackupTime[0]);
            int backupMinute = Convert.ToInt32(arrBackupTime[1]);
            int backupSecond = Convert.ToInt32(arrBackupTime[2]);

            bool isRightDay = now.Day == _appConfig.dayOfMonth;
            bool isRightTime = now.Hour == backupHour &&
                               now.Minute == backupMinute &&
                               now.Second >= backupSecond;
            bool alreadyBackedUpToday = _lastBackupDate.Date == now.Date;

            if (isRightDay && isRightTime && !alreadyBackedUpToday)
            {
                _logger.LogInformation("===========================================");
                _logger.LogInformation("Starting MONTHLY backup at: {time}", DateTimeOffset.Now);

                DateTime lastMonth = now.AddMonths(-1);
                DateTime startOfLastMonth = new DateTime(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0);
                DateTime endOfLastMonth = startOfLastMonth.AddMonths(1).AddSeconds(-1);

                _logger.LogInformation("Backup range: {start} to {end}",
                    startOfLastMonth.ToString("yyyy-MM-dd HH:mm:ss"),
                    endOfLastMonth.ToString("yyyy-MM-dd HH:mm:ss"));

                // ✅ STEP 1: Export to Excel
                ExportExcelMonthly(startOfLastMonth, endOfLastMonth);

                _logger.LogInformation("Monthly backup completed at: {time}", DateTimeOffset.Now);

                // ✅ STEP 2: Auto-delete old data (jika diaktifkan)
                if (_appConfig.autoDeleteAfterBackup)
                {
                    _logger.LogInformation("===========================================");
                    _logger.LogInformation("🗑️  Auto-delete enabled. Starting cleanup...");
                    _logger.LogInformation("Retention period: {months} months", _appConfig.retentionMonths);

                    try
                    {
                        var deleteResult = await _dbHelper.ClearOldHistoricalData(_appConfig.retentionMonths);

                        // ✅ Parse cutoff date dari Ticks
                        DateTime cutoffDate = new DateTime(deleteResult["CutoffDate"]);
                        
                        _logger.LogInformation("Deleting data BEFORE: {date}", cutoffDate.ToString("yyyy-MM-dd"));

                        long totalDeleted = deleteResult["TOTAL"];

                        if (totalDeleted > 0)
                        {
                            _logger.LogInformation("===========================================");
                            _logger.LogInformation("✅ Deleted {count} old historical records", totalDeleted);
                            
                            foreach (var kvp in deleteResult)
                            {
                                if (kvp.Key != "TOTAL" && kvp.Key != "CutoffDate" && kvp.Value > 0)
                                {
                                    _logger.LogInformation("   {tank}: {count} records deleted", kvp.Key, kvp.Value);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️  No old data to delete (all within retention period)");
                        }

                        _logger.LogInformation("===========================================");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("❌ Error during auto-delete: {error}", ex.Message);
                        _logger.LogError("Stack trace: {stack}", ex.StackTrace);
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️  Auto-delete disabled. Data retained in database.");
                }

                _lastBackupDate = now.Date;
                _logger.LogInformation("===========================================");
            }
        }

        private void RunDailyBackup()
        {
            string[] arrStartTime = _appConfig.timeStart.Split(':');
            string[] arrEndTime = _appConfig.timeEnd.Split(':');
            DateTime currTime = DateTime.Now;

            DateTime limitDataTime = DateTime.Today.AddMonths(-1).AddDays(-1);

            if (
                (currTime.Hour >= Convert.ToInt32(arrStartTime[0]) && currTime.Hour <= Convert.ToInt32(arrEndTime[0]))
                &&
                (currTime.Minute >= Convert.ToInt32(arrStartTime[1]) && currTime.Minute <= Convert.ToInt32(arrEndTime[1]))
                &&
                (currTime.Second >= Convert.ToInt32(arrStartTime[2]) && currTime.Second <= Convert.ToInt32(arrEndTime[2]))
                )
            {
                // ✅ FIX: Gunakan method yang benar
                ExportExcelDaily(limitDataTime);
            }
        }

        private void ExportExcelMonthly(DateTime startDate, DateTime endDate)
        {
            try
            {
                var exportFolderName = startDate.ToString("yyyyMM") + "_Monthly_Backup";
                var exportPath = Path.Combine(_appConfig.exportPath, exportFolderName);

                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                var allTank = _dbHelper.GetAllTank();
                _logger.LogInformation("Total tanks to export: {count}", allTank.Count);

                foreach (var tank in allTank)
                {
                    try
                    {
                        var templatePath = $"Doc/TemplateTankHistorical_{tank.Tank_Name}.xlsx";

                        if (!File.Exists(templatePath))
                        {
                            _logger.LogWarning("Template not found for tank {tank}, skipping...", tank.Tank_Name);
                            continue;
                        }

                        FileStream templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
                        IWorkbook workbook = new XSSFWorkbook(templateStream);

                        _logger.LogInformation("Extracting data for {tank} from {start} to {end}...",
                            tank.Tank_Name, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                        var tankHistorical = _dbHelper.GetTankHistoricalByDateRange(tank.Tank_Name, startDate, endDate);

                        if (tankHistorical.Count > 0)
                        {
                            ISheet sheet = workbook.GetSheet(tank.Tank_Name);
                            int rowIndex = 9;
                            int number = 1;

                            foreach (var history in tankHistorical)
                            {
                                var product = _dbHelper.GetProductById(tank.Product_ID);

                                IRow row = sheet.CreateRow(rowIndex);
                                row.CreateCell(0).SetCellValue(number);
                                row.CreateCell(1).SetCellValue(tank.Tank_Name);
                                row.CreateCell(2).SetCellValue(product?.Product_Name ?? "Unknown");
                                // ✅ FIX: Tutup string dengan benar
                                row.CreateCell(3).SetCellValue(Convert.ToDateTime(history.TimeStamp).ToString("yyyy MMMM dd / HH:mm:ss"));
                                row.CreateCell(4).SetCellValue(history.Level ?? 0);
                                row.CreateCell(5).SetCellValue(history.Level_Water ?? 0);
                                row.CreateCell(6).SetCellValue(history.Temperature ?? 0);
                                row.CreateCell(7).SetCellValue(history.Temperature ?? 0);
                                row.CreateCell(8).SetCellValue(history.Density ?? 0);
                                row.CreateCell(9).SetCellValue(history.Volume_Obs ?? 0);
                                row.CreateCell(10).SetCellValue(history.Pumpable ?? 0);
                                row.CreateCell(11).SetCellValue(history.Ullage ?? 0);
                                row.CreateCell(12).SetCellValue(history.Flowrate ?? 0);

                                rowIndex++;
                                number++;
                            }

                            IRow rowDate = sheet.CreateRow(5);
                            rowDate.CreateCell(1).SetCellValue("Date Export : ");
                            rowDate.CreateCell(2).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                            rowDate.CreateCell(3).SetCellValue($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                            var fileName = $"{startDate:yyyyMM}_{tank.Tank_Name}_Monthly_Backup.xlsx";
                            var fullPath = Path.Combine(exportPath, fileName);

                            using (var fs = new FileStream(fullPath, FileMode.Create))
                            {
                                workbook.Write(fs);
                            }

                            _logger.LogInformation("Exported {count} records for {tank}", tankHistorical.Count, tank.Tank_Name);
                        }
                        else
                        {
                            _logger.LogWarning("No data found for {tank} in range {start} to {end}",
                                tank.Tank_Name, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                        }

                        templateStream.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error exporting {tank}: {error}", tank.Tank_Name, ex.Message);
                    }
                }

                _logger.LogInformation("Monthly backup completed: {path}", exportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError("Monthly backup failed: {error}", ex.Message);
            }
        }

        // ✅ FIX: Tambahkan method ExportExcelDaily yang hilang
        private void ExportExcelDaily(DateTime timeRange)
        {
            try
            {
                var exportDateFolder = timeRange.ToString("yyyyMMdd") + "_Daily_Backup";
                var exportPath = Path.Combine(_appConfig.exportPath, exportDateFolder);

                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                var allTank = _dbHelper.GetAllTank();
                _logger.LogInformation("Total tanks to export: {count}", allTank.Count);

                foreach (var t in allTank)
                {
                    try
                    {
                        var templatePath = $"Doc/TemplateTankHistorical_{t.Tank_Name}.xlsx";

                        if (!File.Exists(templatePath))
                        {
                            _logger.LogWarning("Template not found for tank {tank}, skipping...", t.Tank_Name);
                            continue;
                        }

                        FileStream templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
                        IWorkbook workbook = new XSSFWorkbook(templateStream);

                        _logger.LogInformation("Worker running at: {time}; Extracting Data {tank}...", DateTimeOffset.Now, t.Tank_Name);

                        var tankHistorical = _dbHelper.GetTankHistoricalByTankName(timeRange, t.Tank_Name);

                        if (tankHistorical.Count > 0)
                        {
                            int rowIndex = 9;
                            int number = 1;

                            ISheet sheet = workbook.GetSheet(t.Tank_Name);

                            foreach (var history in tankHistorical)
                            {
                                var tank = _dbHelper.GetTankByName(history.Tank_Number);
                                var product = _dbHelper.GetProductById(tank.Product_ID);

                                IRow row = sheet.CreateRow(rowIndex);
                                row.CreateCell(0).SetCellValue(number);
                                row.CreateCell(1).SetCellValue(tank.Tank_Name);
                                row.CreateCell(2).SetCellValue(product?.Product_Name ?? "Unknown");
                                row.CreateCell(3).SetCellValue(Convert.ToDateTime(history.TimeStamp).ToString("yyyy MMMM dd / HH:mm:ss"));
                                row.CreateCell(4).SetCellValue(history.Level ?? 0);
                                row.CreateCell(5).SetCellValue(history.Level_Water ?? 0);
                                row.CreateCell(6).SetCellValue(history.Temperature ?? 0);
                                row.CreateCell(7).SetCellValue(history.Temperature ?? 0);
                                row.CreateCell(8).SetCellValue(history.Density ?? 0);
                                row.CreateCell(9).SetCellValue(history.Volume_Obs ?? 0);
                                row.CreateCell(10).SetCellValue(history.Pumpable ?? 0);
                                row.CreateCell(11).SetCellValue(history.Ullage ?? 0);
                                row.CreateCell(12).SetCellValue(history.Flowrate ?? 0);

                                rowIndex++;
                                number++;
                            }

                            IRow rowDate = sheet.CreateRow(5);
                            rowDate.CreateCell(1).SetCellValue("Date Export : ");
                            rowDate.CreateCell(2).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

                            var exportTime = timeRange.ToString($"yyyyMMdd_{t.Tank_Name}");
                            var path = exportPath;
                            var newFile = $"{exportTime}_Daily_Backup.xlsx";
                            newFile = Path.Combine(path, newFile);

                            using (var fs = new FileStream(newFile, FileMode.Create))
                            {
                                workbook.Write(fs);
                            }

                            _logger.LogInformation("Worker running at: {time}; Data Historical {tank} has been exported", DateTimeOffset.Now, t.Tank_Name);
                        }
                        else
                        {
                            _logger.LogWarning("No historical data found for {tank} on {date}", t.Tank_Name, timeRange.ToString("yyyy-MM-dd"));
                        }

                        templateStream.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Worker running at: {time}; Error Exporting Historical {tank} : {error}", DateTimeOffset.Now, t.Tank_Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Worker running at: {time}; Error in ExportExcelDaily: {error}", DateTimeOffset.Now, ex.Message);
            }
        }
    }
}