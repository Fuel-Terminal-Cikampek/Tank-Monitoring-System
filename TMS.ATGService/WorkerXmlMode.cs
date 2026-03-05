using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TMS.ATGService.Service;
using TMS.Models;

namespace TMS.ATGService
{
    /// <summary>
    /// Background Service untuk polling data dari FD-Interface (HTTP XML Mode)
    ///
    /// FLOWRATE CALCULATION SYSTEM:
    /// =============================
    /// Rumus (Menggunakan RaisePerMM per tangki dari dbo.Tank):
    ///   1. ΔLevel = Level - LastLevel (mm)
    ///   2. VolumeChange = ΔLevel × RaisePerMM (Liter)
    ///   3. L/s = VolumeChange / TotalSeconds
    ///   4. KL/h = (L/s × 3600) / 1000
    ///
    /// Baseline Update:
    ///   - Flowrate HANYA DIHITUNG saat cnt == cntFlowrate (setiap 5 menit)
    ///   - Di antara baseline update: HOLD countdown terjadi setiap cycle
    ///
    /// HOLD Logic:
    ///   - Jika flowrate > 0: HOLD = FlowrateHoldSeconds (dari config)
    ///   - HOLD countdown setiap cycle (HOLD--)
    ///   - Saat HOLD = 0: Flowrate = 0 (tanpa perhitungan baru)
    /// </summary>
    public class WorkerXmlMode : BackgroundService
    {
        private readonly ILogger<WorkerXmlMode> _logger;
        private readonly FDInterfaceConfiguration _config;
        private readonly tankConfiguration _tankConfig;
        private readonly DBHerper _dbHelper;
        private readonly HttpClient _httpClient;

        // ✅ File Logger untuk logging ke file harian
        private readonly FileLogger _fileLogger;

        // =========================
        // STATE VARIABLES
        // =========================
        private int cnt = 0;

        // In-memory state untuk HOLD logic per tank
        private readonly Dictionary<string, TankFlowState> _tankFlowStates
            = new Dictionary<string, TankFlowState>();

        // ✅ NEW: Track previous alarm status untuk deteksi perubahan
        private readonly Dictionary<string, int?> _previousAlarmStatus
            = new Dictionary<string, int?>();

        // ✅ NEW: Track previous alarm condition untuk logging
        private readonly Dictionary<string, string> _previousAlarmCondition
            = new Dictionary<string, string>();

        public WorkerXmlMode(
            ILogger<WorkerXmlMode> logger,
            FDInterfaceConfiguration config,
            tankConfiguration tankConfig)
        {
            _logger = logger;
            _config = config;
            _tankConfig = tankConfig;

            // ✅ Initialize File Logger untuk logging ke file harian
            _fileLogger = new FileLogger();

            // Create DBHerper with logger factory
            var dbLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var dbLogger = dbLoggerFactory.CreateLogger<DBHerper>();
            _dbHelper = new DBHerper(dbLogger);

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // ✅ FIX: Set BaseAddress jika menggunakan HTTP mode
            if (config.UseHttpMode && !string.IsNullOrEmpty(config.HttpEndpoint))
            {
                try
                {
                    // Parse endpoint sebagai absolute URI
                    var uri = new Uri(config.HttpEndpoint);
                    _httpClient.BaseAddress = new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Invalid HTTP endpoint URI");
                }
            }

            _logger.LogInformation("==============================================");
            _logger.LogInformation("TMS.ATGService - FD INTERFACE HTTP MODE");
            _logger.LogInformation($"Polling Interval: {_config.PollingIntervalSeconds} seconds");
            _logger.LogInformation($"HTTP Endpoint   : {_config.HttpEndpoint}");
            _logger.LogInformation($"UseHttpMode     : {_config.UseHttpMode}");
            _logger.LogInformation($"Warmup Cycles   : {_tankConfig.cntFlowrate}");
            _logger.LogInformation($"Log Directory   : {_fileLogger.GetCurrentLogPath()}");
            _logger.LogInformation("==============================================");

            // Log startup info ke file
            Log("══════════════════════════════════════════════════════════════════════════");
            Log("  TMS.ATGService STARTED - FD INTERFACE HTTP MODE");
            Log("══════════════════════════════════════════════════════════════════════════");
            Log($"  Polling Interval  : {_config.PollingIntervalSeconds} seconds");
            Log($"  HTTP Endpoint     : {_config.HttpEndpoint}");
            Log($"  Warmup Cycles     : {_tankConfig.cntFlowrate}");
            Log("──────────────────────────────────────────────────────────────────────────");
            Log("  ZERO PROTECTION CONFIGURATION:");
            Log($"    Max Retry       : {_config.ZeroRetryMaxAttempts} attempts");
            Log($"    Retry Delays    : [{string.Join(", ", _config.ZeroRetryDelaysMs)}] ms");
            Log($"    Hold Cycles     : {_config.ZeroHoldCycles} cycles (~{_config.ZeroHoldCycles * _config.PollingIntervalSeconds}s)");
            Log($"    Max ΔLevel      : {_config.MaxDeltaLevelPerCycle} mm/cycle");
            Log("──────────────────────────────────────────────────────────────────────────");
            Log("  CROSS-REFERENCE VALIDATION:");
            Log($"    Enabled         : {(_config.EnableCrossReferenceValidation ? "YES" : "NO")}");
            Log($"    Temp Range      : {_config.CrossRefMinTemperature}°C - {_config.CrossRefMaxTemperature}°C");
            Log($"    Density Range   : {_config.CrossRefMinDensity} - {_config.CrossRefMaxDensity} kg/L");
            Log("══════════════════════════════════════════════════════════════════════════");
        }

        // =========================================================
        // ✅ HELPER: Write ke Console DAN Log file sekaligus
        // =========================================================
        private void Log(string message)
        {
            Console.WriteLine(message);  // ✅ Output ke Console
            _fileLogger?.LogRaw(message); // ✅ Output ke File
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessHttpData();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in worker loop");
                }

                await Task.Delay(_config.PollingIntervalSeconds * 1000, stoppingToken);
            }
        }

        // =========================================================
        // MAIN HTTP PROCESS
        // =========================================================
        private async Task ProcessHttpData()
        {
            // ✅ Validasi HttpEndpoint
            if (string.IsNullOrEmpty(_config.HttpEndpoint))
            {
                Log("❌ ERROR: HttpEndpoint is not configured!");
                _logger.LogError("HttpEndpoint is empty or null. Please configure FDInterfaceConfiguration:HttpEndpoint");
                return;
            }

            Log("");
            Log("═══════════════════════════════════════════════");
            Log($" NEW CYCLE STARTED - {DateTime.Now:yyyy-MM-dd HH.mm.ss}");
            Log("═══════════════════════════════════════════════");

            try
            {
                var response = await _httpClient.GetAsync(_config.HttpEndpoint);
                response.EnsureSuccessStatusCode();
                var xmlContent = await response.Content.ReadAsStringAsync();

                // ===== XML CONTENT VALIDATION =====
                Log($"[HTTP] Response received, length={xmlContent.Length} bytes");

                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    Log("❌ [HTTP ERROR] XML content is empty or null!");
                    return;
                }

                // Log first/last 100 chars for debugging
                string firstChars = xmlContent.Length > 100 ? xmlContent.Substring(0, 100) : xmlContent;
                string lastChars = xmlContent.Length > 100 ? xmlContent.Substring(xmlContent.Length - 100) : xmlContent;
                Log($"[HTTP] XML First 100 chars: {firstChars.Replace("\n", "\\n").Replace("\r", "\\r")}");
                Log($"[HTTP] XML Last 100 chars: {lastChars.Replace("\n", "\\n").Replace("\r", "\\r")}");

                // Validate XML structure
                if (!xmlContent.TrimStart().StartsWith("<"))
                {
                    Log($"❌ [HTTP ERROR] XML does not start with '<' - Invalid format! First char: '{xmlContent.TrimStart().FirstOrDefault()}'");
                    return;
                }

                if (!xmlContent.TrimEnd().EndsWith(">"))
                {
                    Log($"❌ [HTTP ERROR] XML does not end with '>' - Possibly truncated! Last char: '{xmlContent.TrimEnd().LastOrDefault()}'");
                    return;
                }

                // Minimum length check (valid XML should be at least 100 bytes for system with tanks)
                if (xmlContent.Length < 100)
                {
                    Log($"⚠️ [HTTP WARNING] XML is suspiciously short ({xmlContent.Length} bytes) - Possible incomplete response");
                }

                Log($"✅ [HTTP] XML validation passed");

                var tempFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "fdinterface_temp.xml");

                await System.IO.File.WriteAllTextAsync(tempFile, xmlContent);
                Log($"[FILE] XML written to temp file: {tempFile}");

                var reader = new FDInterfaceXmlReader(tempFile, null);
                var tankLiveDataList = reader.ConvertToTankLiveData();

                if (tankLiveDataList == null || !tankLiveDataList.Any())
                {
                    Log("❌ No tank data received");
                    return;
                }

                Log($"═══════════════════════════════════════════════════");
                Log($"XML Conversion Summary: {tankLiveDataList.Count} tanks converted successfully");
                Log($"═══════════════════════════════════════════════════");
                Log("");

                // =========================================================
                // ✅ MULTIPLE RETRY WITH EXPONENTIAL BACKOFF untuk Level=0
                // =========================================================
                var tanksWithZeroLevel = tankLiveDataList.Where(t => t.Level == 0).ToList();

                if (tanksWithZeroLevel.Any())
                {
                    Log($"");
                    Log($"╔═══════════════════════════════════════════════════════════════╗");
                    Log($"║  ⚠️  ZERO LEVEL DETECTED - MULTIPLE RETRY WITH BACKOFF        ║");
                    Log($"╠═══════════════════════════════════════════════════════════════╣");
                    Log($"║  Tanks with Level=0: {tanksWithZeroLevel.Count,-2}                                        ║");
                    foreach (var t in tanksWithZeroLevel)
                    {
                        Log($"║    - {t.Tank_Number,-10}                                             ║");
                    }
                    Log($"║  Max Retries: {_config.ZeroRetryMaxAttempts}                                                 ║");
                    Log($"║  Delays (ms): [{string.Join(", ", _config.ZeroRetryDelaysMs)}]                                  ║");
                    Log($"╚═══════════════════════════════════════════════════════════════╝");
                    Log($"");

                    // Initialize state untuk semua tank dengan Level=0
                    foreach (var tankWith0 in tanksWithZeroLevel)
                    {
                        if (!_tankFlowStates.ContainsKey(tankWith0.Tank_Number))
                        {
                            _tankFlowStates[tankWith0.Tank_Number] = new TankFlowState();
                        }
                        _tankFlowStates[tankWith0.Tank_Number].RetryAttempted = true;
                        _tankFlowStates[tankWith0.Tank_Number].RetryAttemptCount = 0;
                    }

                    // ✅ MULTIPLE RETRY LOOP dengan exponential backoff
                    int maxRetries = _config.ZeroRetryMaxAttempts;
                    var retryDelays = _config.ZeroRetryDelaysMs ?? new int[] { 100, 300, 500 }; // Default fallback

                    // Safety check: pastikan retryDelays tidak kosong
                    if (retryDelays.Length == 0)
                    {
                        retryDelays = new int[] { 100, 300, 500 };
                        Log($"[RETRY] ⚠️ ZeroRetryDelaysMs is empty, using default [100, 300, 500]");
                    }

                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        // Check jika masih ada tank dengan Level=0
                        var stillZeroTanks = tanksWithZeroLevel.Where(t => t.Level == 0).ToList();
                        if (!stillZeroTanks.Any())
                        {
                            Log($"[RETRY] ✅ All tanks recovered before attempt {attempt}!");
                            break;
                        }

                        // Get delay untuk attempt ini (use last delay jika index out of range)
                        int delayIndex = Math.Min(attempt - 1, retryDelays.Length - 1);
                        int delayMs = retryDelays[delayIndex];

                        Log($"[RETRY] Attempt {attempt}/{maxRetries} - Waiting {delayMs}ms before retry...");
                        Log($"[RETRY] Tanks still at zero: {string.Join(", ", stillZeroTanks.Select(t => t.Tank_Number))}");

                        // Exponential backoff delay
                        await Task.Delay(delayMs);

                        try
                        {
                            // Retry: HTTP GET + Parse
                            var retryResponse = await _httpClient.GetAsync(_config.HttpEndpoint);
                            retryResponse.EnsureSuccessStatusCode();
                            var retryXmlContent = await retryResponse.Content.ReadAsStringAsync();

                            Log($"[RETRY] Attempt {attempt}: Response received, length={retryXmlContent.Length} bytes");

                            var retryTempFile = System.IO.Path.Combine(
                                System.IO.Path.GetTempPath(),
                                $"fdinterface_retry_{attempt}.xml");

                            await System.IO.File.WriteAllTextAsync(retryTempFile, retryXmlContent);

                            var retryReader = new FDInterfaceXmlReader(retryTempFile, null);
                            var retryData = retryReader.ConvertToTankLiveData();

                            // Update tanks yang masih Level=0
                            foreach (var tankWith0 in stillZeroTanks)
                            {
                                var state = _tankFlowStates[tankWith0.Tank_Number];
                                state.RetryAttemptCount = attempt;

                                var retryTank = retryData.FirstOrDefault(t => t.Tank_Number == tankWith0.Tank_Number);

                                if (retryTank != null && retryTank.Level > 0)
                                {
                                    // ✅ RECOVERED
                                    Log($"[RETRY] ✅ Attempt {attempt}: Tank {tankWith0.Tank_Number} RECOVERED: Level={retryTank.Level}mm");

                                    tankWith0.Level = retryTank.Level;
                                    tankWith0.Temperature = retryTank.Temperature;
                                    tankWith0.Density = retryTank.Density;
                                    tankWith0.Volume_Obs = retryTank.Volume_Obs;
                                    tankWith0.Volume_Std = retryTank.Volume_Std;
                                    tankWith0.Level_Water = retryTank.Level_Water;

                                    state.RetrySuccess = true;
                                    state.ZeroReason = $"RECOVERED_BY_RETRY_ATTEMPT_{attempt}";
                                }
                                else if (retryTank != null)
                                {
                                    Log($"[RETRY] ❌ Attempt {attempt}: Tank {tankWith0.Tank_Number} still zero");
                                    state.ZeroReason = $"STILL_ZERO_AFTER_ATTEMPT_{attempt}";
                                }
                                else
                                {
                                    Log($"[RETRY] ⚠️ Attempt {attempt}: Tank {tankWith0.Tank_Number} not found in retry data");
                                    state.ZeroReason = "TANK_NOT_IN_RETRY_DATA";
                                }
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Log($"❌ [RETRY] Attempt {attempt} FAILED: {retryEx.Message}");
                            foreach (var tankWith0 in stillZeroTanks)
                            {
                                var state = _tankFlowStates[tankWith0.Tank_Number];
                                state.ZeroReason = $"RETRY_ATTEMPT_{attempt}_FAILED";
                            }
                        }
                    }

                    // Final summary
                    int recoveredCount = tanksWithZeroLevel.Count(t => t.Level > 0);
                    int stillZeroCount = tanksWithZeroLevel.Count(t => t.Level == 0);

                    // Mark tanks yang tetap zero setelah semua retry
                    foreach (var tankWith0 in tanksWithZeroLevel.Where(t => t.Level == 0))
                    {
                        var state = _tankFlowStates[tankWith0.Tank_Number];
                        state.RetrySuccess = false;
                        if (!state.ZeroReason.Contains("FAILED"))
                        {
                            state.ZeroReason = $"FD_INTERFACE_PERSISTENT_ZERO_AFTER_{maxRetries}_RETRIES";
                        }
                    }

                    Log($"");
                    Log($"╔═══════════════════════════════════════════════════════════════╗");
                    Log($"║  RETRY RESULT SUMMARY (after {maxRetries} attempts)                       ║");
                    Log($"╠═══════════════════════════════════════════════════════════════╣");
                    Log($"║  ✅ Recovered: {recoveredCount,-2} tanks                                        ║");
                    Log($"║  ❌ Still Zero: {stillZeroCount,-2} tanks (will use zero protection)        ║");
                    Log($"╚═══════════════════════════════════════════════════════════════╝");
                    Log($"");
                }

                var dbTanks = _dbHelper.GetTanks();
                var existingLiveData = _dbHelper.GetAllTankLiveData();

                foreach (var liveData in tankLiveDataList)
                {
                    var tank = dbTanks.FirstOrDefault(t => t.Tank_Name == liveData.Tank_Number);
                    if (tank == null) continue;

                    var existing = existingLiveData
                        .FirstOrDefault(t => t.Tank_Number == liveData.Tank_Number);

                    if (existing != null)
                    {
                        liveData.LastVolume = existing.LastVolume;
                        liveData.LastLiquidLevel = existing.LastLiquidLevel;
                        liveData.LastTimeStamp = existing.LastTimeStamp;
                        liveData.Flowrate = existing.Flowrate;
                        liveData.TotalSecond = existing.TotalSecond; // Restore TotalSecond dari DB
                        // ✅ FIX: Restore Ack dari database (PENTING! Jangan overwrite dengan default value!)
                        liveData.Ack = existing.Ack;
                    }

                    // ✅ Initialize state jika belum ada
                    if (!_tankFlowStates.ContainsKey(liveData.Tank_Number))
                    {
                        _tankFlowStates[liveData.Tank_Number] = new TankFlowState();
                    }

                    // ✅ Restore state dari memory (PENTING!)
                    var tankState = _tankFlowStates[liveData.Tank_Number];
                    liveData.FlowrateHoldRemaining = tankState.HoldRemaining;
                    liveData.LastValidFlowrate = tankState.LastValidFlowrate;

                    // =========================================================
                    // ✅ ENHANCED ZERO PROTECTION LOGIC
                    //    - Configurable hold cycles
                    //    - Rate of Change Validation
                    //    - Cross-Reference Validation (Temp/Density)
                    // =========================================================
                    double currentLevel = liveData.Level ?? 0;
                    double previousLevel = existing?.Level ?? 0;
                    double currentTemp = liveData.Temperature ?? 0;
                    double currentDensity = liveData.Density ?? 0;

                    int zeroHoldCycles = _config.ZeroHoldCycles;

                    // =========================================================
                    // 🔴 RATE OF CHANGE VALIDATION
                    // Check if level change is physically impossible
                    // =========================================================
                    bool rateOfChangeViolation = false;
                    double deltaLevel = Math.Abs(currentLevel - previousLevel);

                    if (previousLevel > 0 && currentLevel >= 0 && deltaLevel > _config.MaxDeltaLevelPerCycle)
                    {
                        rateOfChangeViolation = true;
                        Log($"");
                        Log($"🚨🚨🚨 [RATE OF CHANGE VIOLATION] Tank={liveData.Tank_Number} 🚨🚨🚨");
                        Log($"  - Previous Level: {previousLevel}mm");
                        Log($"  - Current Level:  {currentLevel}mm");
                        Log($"  - Delta Level:    {deltaLevel:F1}mm");
                        Log($"  - Max Allowed:    {_config.MaxDeltaLevelPerCycle}mm per cycle");
                        Log($"  - Verdict: INVALID READING - Level change too drastic!");
                        Log($"  - Action: 🔒 HOLD - Using previous level from DB");
                        Log($"");

                        // Mark as invalid and use previous level
                        tankState.RateOfChangeViolationCount++;
                        tankState.ZeroReason = $"RATE_OF_CHANGE_VIOLATION_DELTA_{deltaLevel:F0}mm";
                        // Safe null check - use previous level from DB if available
                        if (existing != null && existing.Level.HasValue)
                        {
                            liveData.Level = existing.Level.Value;
                        }
                        // else: keep currentLevel (risky tapi tidak ada pilihan lain)
                    }
                    else if (tankState.RateOfChangeViolationCount > 0 && deltaLevel <= _config.MaxDeltaLevelPerCycle)
                    {
                        // Reset violation count jika kembali normal
                        Log($"[RATE CHECK] Tank={liveData.Tank_Number} - Rate of change back to normal (Δ{deltaLevel:F1}mm)");
                        tankState.RateOfChangeViolationCount = 0;
                    }

                    // =========================================================
                    // 🟡 CROSS-REFERENCE VALIDATION
                    // If Level=0 but Temp/Density are normal, likely comm error
                    // =========================================================
                    bool crossRefIndicatesCommError = false;

                    if (currentLevel == 0 && _config.EnableCrossReferenceValidation && !rateOfChangeViolation)
                    {
                        bool tempIsNormal = currentTemp >= _config.CrossRefMinTemperature &&
                                           currentTemp <= _config.CrossRefMaxTemperature;
                        bool densityIsNormal = currentDensity >= _config.CrossRefMinDensity &&
                                              currentDensity <= _config.CrossRefMaxDensity;

                        if (tempIsNormal && densityIsNormal)
                        {
                            crossRefIndicatesCommError = true;
                            Log($"");
                            Log($"🔍🔍🔍 [CROSS-REFERENCE CHECK] Tank={liveData.Tank_Number} 🔍🔍🔍");
                            Log($"  - Level: 0mm (ZERO)");
                            Log($"  - Temperature: {currentTemp:F2}°C (Normal range: {_config.CrossRefMinTemperature}-{_config.CrossRefMaxTemperature}°C) ✅");
                            Log($"  - Density: {currentDensity:F4} kg/L (Normal range: {_config.CrossRefMinDensity}-{_config.CrossRefMaxDensity}) ✅");
                            Log($"  - Verdict: LIKELY COMMUNICATION ERROR (Temp/Density normal but Level=0)");
                            Log($"  - Note: Real empty tank would have abnormal temp/density readings");
                            Log($"");

                            tankState.CrossRefViolationCount++;
                            if (string.IsNullOrEmpty(tankState.ZeroReason) || tankState.ZeroReason == "XML_PARSE_RETURNED_ZERO")
                            {
                                tankState.ZeroReason = "CROSS_REF_LIKELY_COMM_ERROR";
                            }
                        }
                        else if (!tempIsNormal || !densityIsNormal)
                        {
                            // Temp/Density juga abnormal → mungkin benar-benar kosong atau sensor bermasalah
                            Log($"");
                            Log($"🔍 [CROSS-REFERENCE CHECK] Tank={liveData.Tank_Number}");
                            Log($"  - Level: 0mm, Temp: {currentTemp:F2}°C ({(tempIsNormal ? "✅" : "❌")}), Density: {currentDensity:F4} ({(densityIsNormal ? "✅" : "❌")})");
                            Log($"  - Verdict: POSSIBLY LEGITIMATE ZERO (Temp/Density also abnormal)");
                            Log($"");
                        }
                    }

                    // =========================================================
                    // ZERO LEVEL HOLD LOGIC (dengan configurable cycles)
                    // =========================================================
                    if (currentLevel == 0 && !rateOfChangeViolation)
                    {
                        // ✅ Track diagnostic info saat pertama kali detect 0
                        if (tankState.ZeroCount == 0)
                        {
                            tankState.FirstZeroTime = DateTime.Now;
                            tankState.LastValidLevel = existing?.Level;

                            // Determine initial reason jika belum di-set
                            if (string.IsNullOrEmpty(tankState.ZeroReason))
                            {
                                if (!tankState.RetryAttempted)
                                {
                                    tankState.ZeroReason = "XML_PARSE_RETURNED_ZERO";
                                }
                            }
                        }

                        tankState.ZeroCount++;

                        Log($"");
                        Log($"⚠️⚠️⚠️ [ZERO DETECTED] Tank={liveData.Tank_Number} ⚠️⚠️⚠️");
                        Log($"  - Zero Count: {tankState.ZeroCount}/{zeroHoldCycles}");
                        Log($"  - Previous Level (DB): {existing?.Level?.ToString() ?? "NULL"}mm");
                        Log($"  - Last Valid Level: {tankState.LastValidLevel?.ToString() ?? "NULL"}mm");
                        Log($"  - First Zero Time: {tankState.FirstZeroTime?.ToString("HH:mm:ss") ?? "N/A"}");
                        Log($"  - Reason: {tankState.ZeroReason}");
                        Log($"  - Retry Attempts: {tankState.RetryAttemptCount}/{_config.ZeroRetryMaxAttempts}");
                        Log($"  - Retry Success: {(tankState.RetrySuccess ? "Yes" : "No")}");
                        Log($"  - Cross-Ref Violation: {tankState.CrossRefViolationCount}");
                        Log($"  - Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

                        if (tankState.ZeroCount < zeroHoldCycles)
                        {
                            // 🔒 HOLD - pakai level dari DB (ignore race condition)
                            if (existing != null && existing.Level.HasValue)
                            {
                                liveData.Level = existing.Level;
                                Log($"  - Action: 🔒 HOLD - Using last level from DB ({existing.Level}mm)");
                                Log($"  - Hold Reason: Zero count below threshold ({tankState.ZeroCount} < {zeroHoldCycles})");
                                if (crossRefIndicatesCommError)
                                {
                                    Log($"  - Cross-Ref: Likely communication error (extending hold)");
                                }
                            }
                            else
                            {
                                tankState.ZeroReason = "NO_PREVIOUS_LEVEL_IN_DB";
                                Log($"  - Action: ⚠️ CANNOT HOLD - No previous level in DB!");
                                Log($"  - Reason Updated: {tankState.ZeroReason}");
                                Log($"  - Result: Level will be saved as 0 (CRITICAL!)");
                            }
                        }
                        else
                        {
                            // ✅ UPDATE ke 0 (legitimate atau persistent issue setelah zeroHoldCycles)
                            if (tankState.ZeroReason != "LEGITIMATE_EMPTY_TANK" && !tankState.ZeroReason.Contains("CROSS_REF"))
                            {
                                tankState.ZeroReason = $"CONFIRMED_ZERO_AFTER_{zeroHoldCycles}_CYCLES";
                            }
                            Log($"  - Action: ✅ CONFIRMED ZERO - Updating DB to 0");
                            Log($"  - Final Reason: {tankState.ZeroReason}");
                            Log($"  - Duration: ~{tankState.ZeroCount * _config.PollingIntervalSeconds} seconds");
                            Log($"  ⚠️⚠️⚠️ CRITICAL: This may be a persistent parse failure or FD-Interface issue! ⚠️⚠️⚠️");
                        }
                        Log($"");
                    }
                    else if (!rateOfChangeViolation)
                    {
                        // ✅ RESET zero count - level normal (dan bukan rate violation)
                        if (tankState.ZeroCount > 0)
                        {
                            Log($"[ZERO PROTECT] Tank={liveData.Tank_Number}, Level={currentLevel}mm ✅ RECOVERED from {tankState.ZeroCount} consecutive zero readings (duration: ~{tankState.ZeroCount * _config.PollingIntervalSeconds}s)");
                            Log($"               Previous Reason: {tankState.ZeroReason}");
                        }
                        // Reset all diagnostic info
                        tankState.ZeroCount = 0;
                        tankState.ZeroReason = "";
                        tankState.FirstZeroTime = null;
                        tankState.LastValidLevel = currentLevel;
                        tankState.RetryAttempted = false;
                        tankState.RetrySuccess = false;
                        tankState.RetryAttemptCount = 0;
                        tankState.CrossRefViolationCount = 0;
                    }

                    if (cnt >= _tankConfig.cntFlowrate)
                    {
                        CalculateFlowRate(liveData, tank);  // Hitung dan update baseline (pass tank object)
                    }
                    else
                    {
                        // Pertahankan flowrate dari HOLD logic
                        if (liveData.FlowrateHoldRemaining > 0 && liveData.LastValidFlowrate.HasValue)
                        {
                            liveData.Flowrate = liveData.LastValidFlowrate.Value;
                            liveData.FlowrateHoldRemaining--;
                            Log($"   [FLOW] Tank={liveData.Tank_Number}, cnt={cnt}, 🔒 HOLD ACTIVE - Remaining={liveData.FlowrateHoldRemaining}");
                        }
                        else
                        {
                            liveData.Flowrate = 0;
                            Log($"   [FLOW] Tank={liveData.Tank_Number}, cnt={cnt}, ⏸️ WAITING FOR BASELINE UPDATE (cnt={cnt}/{_tankConfig.cntFlowrate})");
                        }
                    }

                    // ✅ Simpan state ke memory (PENTING!)
                    tankState.HoldRemaining = liveData.FlowrateHoldRemaining;
                    tankState.LastValidFlowrate = liveData.LastValidFlowrate;
                    // ZeroCount sudah diupdate di logic protection di atas, tidak perlu set lagi

                    // ========================================
                    // ULLAGE & PUMPABLE CALCULATION
                    // ========================================
                    // Ullage: Ruang kosong tank (LITER)
                    // Formula: Ullage = Volume_Max - Volume_Current
                    // - Volume_Max dari dbo.Tank.Height_Vol_Max (LITER)
                    // - Volume_Current dari FD-Interface Volume_Obs (LITER)
                    double tankVolumeMax = tank.Height_Vol_Max;  // LITER - dari dbo.Tank
                    liveData.Ullage = tankVolumeMax - (liveData.Volume_Obs ?? 0);

                    // Pumpable: Volume yang bisa dipompa (LITER)
                    // Formula: Pumpable = Volume_Current - Deadstock_Volume
                    // - Deadstock_Volume dari dbo.Tank.Deadstock_Volume (LITER)
                    double deadstockVolume = tank.Deadstock_Volume ?? 0;  // LITER - dari dbo.Tank
                    liveData.Pumpable = (liveData.Volume_Obs ?? 0) - deadstockVolume;

                    // ========================================
                    // ALARM MANAGEMENT (berdasarkan kode referensi Worker.cs)
                    // ========================================
                    ProcessAlarmManagement(liveData, tank, existing);

                    // ✅ UPDATE Tank_LiveData (main table - with all columns)
                    _dbHelper.updateTnkLiveData(liveData);

                    // ✅ NEW: UPDATE Tank_LiveDataTMS (clean table - 19 columns only)
                    // Parallel transaction - jika gagal, tidak block Tank_LiveData update
                    _dbHelper.updateTankLiveDataTMS(liveData);

                    Log($"[{liveData.Tank_Number}] Flowrate = {liveData.Flowrate:F2} KL/h");
                }

                try { System.IO.File.Delete(tempFile); } catch { }

                // =========================
                // ✅ COUNTER MANAGEMENT
                // =========================
                // Counter untuk baseline flowrate update
                // Reset setiap mencapai threshold (cntFlowrate dari config)
                cnt += 1;
                if (cnt > _tankConfig.cntFlowrate)
                {
                    cnt = 1;  // Reset ke 1 (bukan 0, karena baru increment)
                }

                Log($"[COUNTER] cnt={cnt}/{_tankConfig.cntFlowrate}");

                // =========================
                // ✅ ZERO DETECTION SUMMARY (Enhanced with Rate/CrossRef info)
                // =========================
                var tanksWithZero = _tankFlowStates
                    .Where(kvp => kvp.Value.ZeroCount > 0 || kvp.Value.RateOfChangeViolationCount > 0)
                    .OrderByDescending(kvp => kvp.Value.ZeroCount)
                    .ThenByDescending(kvp => kvp.Value.RateOfChangeViolationCount)
                    .ToList();

                int holdCycles = _config.ZeroHoldCycles;

                if (tanksWithZero.Any())
                {
                    Log("");
                    Log("╔══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
                    Log("║                              ⚠️  ZERO LEVEL / VALIDATION ALERT                                                   ║");
                    Log("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                    Log($"║  Config: HoldCycles={holdCycles}, MaxRetry={_config.ZeroRetryMaxAttempts}, MaxΔLevel={_config.MaxDeltaLevelPerCycle}mm, CrossRef={(_config.EnableCrossReferenceValidation ? "ON" : "OFF")}     ║");
                    Log("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                    Log("║  Tank       │ Zero  │ Status       │ Last Level │ First Zero   │ Retry │ RateViol │ XRef │ Reason               ║");
                    Log("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");

                    foreach (var tank in tanksWithZero)
                    {
                        var state = tank.Value;
                        string status = state.ZeroCount < holdCycles ? "🔒 HOLD" : "✅ CONFIRMED";
                        if (state.RateOfChangeViolationCount > 0 && state.ZeroCount == 0)
                        {
                            status = "🚨 RATE-ERR";
                        }
                        string lastLevel = state.LastValidLevel?.ToString("F1") ?? "N/A";
                        string firstZero = state.FirstZeroTime?.ToString("HH:mm:ss") ?? "N/A";
                        string retry = state.RetryAttempted ? $"{state.RetryAttemptCount}/{_config.ZeroRetryMaxAttempts}" : "—";
                        string rateViol = state.RateOfChangeViolationCount > 0 ? $"{state.RateOfChangeViolationCount}" : "—";
                        string crossRef = state.CrossRefViolationCount > 0 ? $"{state.CrossRefViolationCount}" : "—";
                        string reason = state.ZeroReason.Length > 20 ? state.ZeroReason.Substring(0, 20) : state.ZeroReason;

                        Log($"║  {tank.Key,-10} │ {state.ZeroCount,2}/{holdCycles,-2} │ {status,-12} │ {lastLevel,7} mm │ {firstZero,-12} │ {retry,-5} │    {rateViol,-3}   │  {crossRef,-2}  │ {reason,-20} ║");
                    }

                    Log("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                    Log("║  REASON CODES:                                                                                                   ║");
                    Log("║    XML_PARSE_RETURNED_ZERO        = FD-Interface returned 0, no retry attempted                                  ║");
                    Log("║    FD_INTERFACE_PERSISTENT_ZERO   = Retry juga return 0 (persistent issue)                                       ║");
                    Log("║    RATE_OF_CHANGE_VIOLATION       = Level berubah terlalu drastis (likely sensor error)                          ║");
                    Log("║    CROSS_REF_LIKELY_COMM_ERROR    = Level=0 tapi Temp/Density normal (likely comm error)                         ║");
                    Log($"║    CONFIRMED_ZERO_AFTER_{holdCycles}_CYCLES = Level 0 dikonfirmasi setelah {holdCycles} cycle                                      ║");
                    Log("╚══════════════════════════════════════════════════════════════════════════════════════════════════════════════════╝");
                    Log("");
                }
            }

            catch (UriFormatException ex)
            {
                _logger.LogError(ex, $"Invalid URI format: {_config.HttpEndpoint}");
                Log($"❌ ERROR: Invalid HTTP endpoint URI: {_config.HttpEndpoint}");
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed");
                Log($"❌ HTTP Error: {ex.Message}");
                return;
            }
        }

        // =========================================================
        // FLOWRATE LOGIC - FIXED VERSION
        // =========================================================
        // Konsep FINAL:
        // - Baseline disimpan di _tankFlowStates (memory), BUKAN di liveData
        // - Karena liveData.LastLiquidLevel dan LastTimeStamp adalah [NotMapped],
        //   nilai-nilai tersebut tidak tersimpan ke database
        // - Flowrate HANYA DIHITUNG saat cnt==cntFlowrate (setiap 5 menit)
        // - HOLD countdown: Terjadi SETIAP cycle (cnt=1-99)
        // =========================================================
        private void CalculateFlowRate(TankLiveData liveData, Tank tank)
        {
            liveData.TimeStamp = DateTime.Now;

            // ✅ GET OR CREATE FLOW STATE FOR THIS TANK (PENTING!)
            if (!_tankFlowStates.ContainsKey(liveData.Tank_Number))
            {
                _tankFlowStates[liveData.Tank_Number] = new TankFlowState();
            }
            var flowState = _tankFlowStates[liveData.Tank_Number];

            // ✅ Hitung HOLD_MAX dari config (auto-convert detik ke cycle)
            int HOLD_MAX_CYCLES = _tankConfig.FlowrateHoldSeconds / _config.PollingIntervalSeconds;
            if (HOLD_MAX_CYCLES < 1) HOLD_MAX_CYCLES = 1; // Minimal 1 cycle

            Log($"   [FLOW] Tank={liveData.Tank_Number}, cnt={cnt}/{_tankConfig.cntFlowrate}, HOLD_MAX={HOLD_MAX_CYCLES} cycles");

            // =========================
            // ✅ CEK APAKAH WAKTUNYA UPDATE BASELINE (setiap cntFlowrate cycles)
            // =========================
            bool shouldUpdateBaseline = (cnt == _tankConfig.cntFlowrate);

            // =========================
            // ✅ FIX: VALIDATION - Cek dari flowState (memory), BUKAN dari liveData
            // =========================
            if (!flowState.IsBaselineInitialized || 
                !flowState.BaselineTimeStamp.HasValue || 
                !flowState.BaselineLiquidLevel.HasValue)
            {
                // ❌ Belum ada baseline - set flowrate=0 dan siapkan baseline
                liveData.Flowrate = 0;
                liveData.FlowrateHoldRemaining = 0;

                // ✅ Initialize baseline di MEMORY (flowState)
                flowState.BaselineLiquidLevel = liveData.Level;
                flowState.BaselineTimeStamp = liveData.TimeStamp;
                flowState.BaselineVolume = liveData.Volume_Obs;
                flowState.IsBaselineInitialized = true;

                // ✅ Also update liveData untuk database storage (akan disimpan ke Tank_LiveDataTMS)
                liveData.LastLiquidLevel = (double?)liveData.Level;
                liveData.LastTimeStamp = liveData.TimeStamp;
                liveData.LastVolume = liveData.Volume_Obs;

                Log($"   [FLOW] ✅ INITIALIZING BASELINE - Level={liveData.Level}mm, Time={liveData.TimeStamp:HH:mm:ss}");
                return;
            }

            // =========================
            // ✅ HITUNG FLOWRATE BARU (HANYA SAAT cnt == cntFlowrate)
            // =========================
            if (shouldUpdateBaseline)
            {
                Log($"   [FLOW] 🔄 BASELINE UPDATE TIME (setiap {_tankConfig.cntFlowrate} cycles = {_tankConfig.cntFlowrate * _config.PollingIntervalSeconds}s)");

                // ✅ FIX: Hitung durasi dari baseline di MEMORY (flowState)
                int totalSecond = (int)(liveData.TimeStamp.Value - flowState.BaselineTimeStamp.Value).TotalSeconds;

                // ✅ Simpan TotalSecond ke liveData agar tersimpan di database
                liveData.TotalSecond = totalSecond;
                Log($"   [DEBUG] TotalSecond = {totalSecond}s");

                if (totalSecond <= 0)
                {
                    Log("   [FLOW] ❌ INVALID TIME DELTA (totalSecond <= 0)");
                    return;
                }

                // =========================================================
                // RUMUS FLOWRATE (Menggunakan RaisePerMM dari dbo.Tank)
                // =========================================================

                // 1. ΔLevel = Level - LastLevel (mm)
                // 2. VolumeChange = ΔLevel × RaisePerMM (Liter)
                // 3. L/s = VolumeChange / TotalSeconds
                // 4. KL/h = (L/s × 3600) / 1000
                // =========================================================

                // ✅ FIX: STEP 1 - Hitung ΔLevel dari flowState (MEMORY), bukan liveData
                double currentLevel = liveData.Level ?? 0;
                double lastLevel = flowState.BaselineLiquidLevel ?? 0;
                double deltaLevel = currentLevel - lastLevel;

                Log($"   [FLOW] Level: {lastLevel}mm → {currentLevel}mm (ΔLevel={deltaLevel:F2}mm)");

                // STEP 2: Hitung FlowRateMperSecond (mm/s) untuk keperluan lain
                double flowRateMperSecond = GetMiliMeterPerSecond(lastLevel, currentLevel, totalSecond);
                liveData.FlowRateMperSecond = flowRateMperSecond;

                // STEP 3 & 4: Hitung Flowrate KL/h menggunakan RaisePerMM
                double raisePerMM = tank.RaisePerMM; // Ltr/mm spesifik per tangki
                double newFlowrate = CalculateFlowrateKLperH(deltaLevel, raisePerMM, totalSecond);

                // Logging detail perhitungan
                double volumeChange = deltaLevel * raisePerMM;
                Log($"   [FLOW] RaisePerMM: {raisePerMM:F2} Ltr/mm (dari dbo.Tank [{tank.Tank_Name}])");
                Log($"   [FLOW] VolumeChange: {deltaLevel:F2}mm × {raisePerMM:F2} = {volumeChange:F2} Liter");

                if (Math.Abs(newFlowrate) > 0.001)
                {
                    double literPerSecond = volumeChange / totalSecond;
                    Log($"   [FLOW] L/s: {volumeChange:F2} / {totalSecond}s = {literPerSecond:F6} L/s");
                    Log($"   [FLOW] KL/h: ({literPerSecond:F6} × 3600) / 1000 = {newFlowrate:F3} KL/h");
                }
                else
                {
                    Log($"   [FLOW] No significant change → Flowrate = 0 KL/h");
                }

                // =========================
                // SET FLOWRATE BARU
                // =========================
                if (Math.Abs(newFlowrate) > 0.001)
                {
                    // ✅ EVENT: NEW FLOWRATE (ada perubahan volume)
                    liveData.Flowrate = newFlowrate;
                    liveData.LastValidFlowrate = newFlowrate;
                    liveData.FlowrateHoldRemaining = HOLD_MAX_CYCLES;
                    liveData.FlowrateJustCalculated = true;

                    // ✅ Update flowState (memory)
                    flowState.LastValidFlowrate = newFlowrate;
                    flowState.HoldRemaining = HOLD_MAX_CYCLES;

                    Log($"   [FLOW] ✓ NEW FLOWRATE = {newFlowrate:F3} KL/h, HOLD={HOLD_MAX_CYCLES} cycles ({_tankConfig.FlowrateHoldSeconds}s)");
                }
                else
                {
                    // ✅ ΔVolume = 0 (tidak ada perubahan volume saat baseline update)
                    liveData.Flowrate = 0;
                    liveData.LastValidFlowrate = null;
                    liveData.FlowrateHoldRemaining = 0;
                    liveData.FlowrateJustCalculated = false;

                    // ✅ Update flowState (memory)
                    flowState.LastValidFlowrate = null;
                    flowState.HoldRemaining = 0;

                    Log("   [FLOW] ⛔ NO CHANGE → FLOWRATE = 0 (no HOLD)");
                }

                // ✅ FIX: UPDATE BASELINE DI MEMORY (flowState) - PENTING!
                flowState.BaselineLiquidLevel = liveData.Level;
                flowState.BaselineTimeStamp = liveData.TimeStamp;
                flowState.BaselineVolume = liveData.Volume_Obs;

                // ✅ Also update liveData untuk database (akan disimpan ke Tank_LiveDataTMS)
                liveData.LastLiquidLevel = (double?)liveData.Level;
                liveData.LastTimeStamp = liveData.TimeStamp;
                liveData.LastVolume = liveData.Volume_Obs;

                Log($"   [FLOW] 📊 BASELINE UPDATED IN MEMORY - Next calculation in {_tankConfig.cntFlowrate * _config.PollingIntervalSeconds}s");
            }
            else
            {
                // =========================
                // ⏸️ BUKAN WAKTU BASELINE UPDATE - HOLD COUNTDOWN
                // =========================
                // HOLD countdown terjadi di SETIAP cycle (cnt=1-99)
                // Saat HOLD=0 → flowrate=0 (tanpa perhitungan baru)

                // ✅ FIX: Gunakan flowState untuk HOLD countdown
                if (flowState.HoldRemaining > 0 && flowState.LastValidFlowrate.HasValue)
                {
                    // 🔒 HOLD COUNTDOWN - pertahankan flowrate, kurangi counter
                    liveData.Flowrate = flowState.LastValidFlowrate.Value;
                    flowState.HoldRemaining--;
                    liveData.FlowrateHoldRemaining = flowState.HoldRemaining;
                    liveData.LastValidFlowrate = flowState.LastValidFlowrate;

                    int secondsUntilNextBaseline = (_tankConfig.cntFlowrate - cnt) * _config.PollingIntervalSeconds;
                    Log($"   [FLOW] 🔒 HOLD COUNTDOWN - Flowrate={liveData.Flowrate:F2} KL/h, HOLD={flowState.HoldRemaining} cycles (next calc in {secondsUntilNextBaseline}s)");
                }
                else
                {
                    // ⏔ HOLD EXPIRED atau tidak ada - flowrate = 0
                    liveData.Flowrate = 0;
                    liveData.LastValidFlowrate = null;
                    liveData.FlowrateHoldRemaining = 0;

                    flowState.LastValidFlowrate = null;
                    flowState.HoldRemaining = 0;

                    int secondsUntilNextBaseline = (_tankConfig.cntFlowrate - cnt) * _config.PollingIntervalSeconds;
                    Log($"   [FLOW] ⏸️ HOLD EXPIRED → Flowrate=0.00 KL/h (next calc in {secondsUntilNextBaseline}s)");
                }
            }
        }

        private void UpdateLastValues(TankLiveData liveData)
        {
            // ✅ Update liveData properties (untuk database Tank_LiveDataTMS)
            liveData.LastLiquidLevel = (double?)liveData.Level;
            liveData.LastVolume = liveData.Volume_Obs;
            liveData.LastTimeStamp = liveData.TimeStamp;

            // ✅ FIX: Also update flowState (memory) untuk persistence
            if (_tankFlowStates.ContainsKey(liveData.Tank_Number))
            {
                var flowState = _tankFlowStates[liveData.Tank_Number];
                flowState.BaselineLiquidLevel = liveData.Level;
                flowState.BaselineVolume = liveData.Volume_Obs;
                flowState.BaselineTimeStamp = liveData.TimeStamp;
                flowState.IsBaselineInitialized = true;
            }
        }

        // =========================================================
        // FLOWRATE CALCULATION HELPERS
        // =========================================================

        /// <summary>
        /// Hitung flowrate dalam mm/s dari perubahan level
        /// Rumus: ΔLevel / TotalSeconds
        /// </summary>
        static double GetMiliMeterPerSecond(double lastLevel, double currentLevel, int totalSecond)
        {
            var deltaLevel = currentLevel - lastLevel;
            if (Math.Abs(deltaLevel) < 0.01) return 0;

            return deltaLevel / totalSecond;  // mm/s
        }

        /// <summary>
        /// Hitung flowrate dalam KL/h dari perubahan level dan RaisePerMM
        /// Rumus:
        ///   1. ΔLevel = Level - LastLevel (mm)
        ///   2. VolumeChange = ΔLevel × RaisePerMM (Liter)
        ///   3. L/s = VolumeChange / TotalSeconds
        ///   4. KL/h = (L/s × 3600) / 1000
        /// </summary>
        static double CalculateFlowrateKLperH(double deltaLevel, double raisePerMM, int totalSecond)
        {
            // STEP 1: Hitung Volume Change dalam Liter
            double volumeChange_Liter = deltaLevel * raisePerMM;

            if (Math.Abs(volumeChange_Liter) < 0.01) return 0;

            // STEP 2: Hitung L/s
            double literPerSecond = volumeChange_Liter / totalSecond;

            // STEP 3: Konversi L/s ke KL/h
            // (L/s × 3600 detik/jam) / 1000 L/KL
            return (literPerSecond * 3600.0) / 1000.0;
        }

        // =========================================================
        // ALARM MANAGEMENT LOGIC (sesuai kode referensi Worker.cs)
        // =========================================================
        private void ProcessAlarmManagement(TankLiveData liveData, Tank tank, TankLiveData existing)
        {
            try
            {
                // ✅ Ambil status alarm dari existing data
                bool ackFromDb = existing?.Ack ?? true;

                // ✅ Ambil level dan temperature saat ini
                double currentLevel = (double)(liveData.Level ?? 0);
                double currentTemp = liveData.Temperature ?? 0;

                // ✅ Cek apakah liquid level berubah (untuk alarm re-trigger)
                double lastLevel = existing?.Level ?? 0;
                bool hasLiquidLevelChanged = Math.Abs(currentLevel - lastLevel) >= 1.0;

                // ✅ DEBUG LOGGING
                Log($"[ALARM-DEBUG] Tank={liveData.Tank_Number}");
                Log($"  Current Level: {currentLevel}mm, Last Level: {lastLevel}mm");
                Log($"  Level Changed: {hasLiquidLevelChanged}");
                Log($"  Thresholds: LL={tank.LevelLL}mm (use={tank.UseAlarmLL}), L={tank.LevelL}mm (use={tank.UseAlarmL}), H={tank.LevelH}mm (use={tank.UseAlarmH}), HH={tank.LevelHH}mm (use={tank.UseAlarmHH})");

                // ✅ Definisi alarm levels dengan NULL SAFETY
                var alarmLevels = new Dictionary<string, (double threshold, bool useAlarm, Func<double, double, bool> condition)>
                {
                    ["HH"] = (
                        tank.LevelHH ?? double.MaxValue,
                        tank.UseAlarmHH ?? false,
                        (level, th) => (tank.UseAlarmHH ?? false) && level >= th
                    ),
                    ["H"] = (
                        tank.LevelH ?? double.MaxValue,
                        tank.UseAlarmH ?? false,
                        (level, th) => {
                            if (!(tank.UseAlarmH ?? false)) return false;
                            bool aboveH = level >= th;
                            bool belowHH = !(tank.UseAlarmHH ?? false) || level < (tank.LevelHH ?? double.MaxValue);
                            return aboveH && belowHH;
                        }
                    ),
                    ["LL"] = (
                        tank.LevelLL ?? 0,
                        tank.UseAlarmLL ?? false,
                        (level, th) => (tank.UseAlarmLL ?? false) && level <= th
                    ),
                    ["L"] = (
                        tank.LevelL ?? 0,
                        tank.UseAlarmL ?? false,
                        (level, th) => {
                            if (!(tank.UseAlarmL ?? false)) return false;
                            bool belowL = level <= th;
                            bool aboveLL = !(tank.UseAlarmLL ?? false) || level > (tank.LevelLL ?? 0);
                            return belowL && aboveLL;
                        }
                    )
                };

                // ✅ Definisi alarm temperature
                var tempAlarms = new Dictionary<string, (double threshold, bool useAlarm, Func<double, double, bool> condition)>
                {
                    ["TempH"] = (
                        tank.TempH ?? double.MaxValue,
                        tank.UseAlarmTempH ?? false,
                        (temp, th) => (tank.UseAlarmTempH ?? false) && temp >= th
                    ),
                    ["TempL"] = (
                        tank.TempL ?? 0,
                        tank.UseAlarmTempL ?? false,
                        (temp, th) => (tank.UseAlarmTempL ?? false) && temp <= th
                    )
                };

                // ✅ Cari alarm level condition yang SEDANG AKTIF
                var levelAlarm = alarmLevels
                    .Where(kvp => kvp.Value.useAlarm && kvp.Value.condition(currentLevel, kvp.Value.threshold))
                    .OrderByDescending(kvp => kvp.Key switch {
                        "HH" => 4,
                        "H" => 3,
                        "L" => 2,
                        "LL" => 1,
                        _ => 0
                    })
                    .FirstOrDefault();

                // ✅ Cari alarm temperature condition yang SEDANG AKTIF
                var tempAlarm = tempAlarms
                    .Where(kvp => kvp.Value.useAlarm && kvp.Value.condition(currentTemp, kvp.Value.threshold))
                    .OrderByDescending(kvp => kvp.Key == "TempH" ? 2 : 1)
                    .FirstOrDefault();

                // ✅ Tentukan kondisi alarm SAAT INI (prioritas: Temperature > Level)
                string currentAlarmCondition = tempAlarm.Key ?? levelAlarm.Key;
                int? currentAlarmStatus = currentAlarmCondition != null ? ConvertAlarmStringToInt(currentAlarmCondition) : (int?)null;

                Log($"  Detected Alarm: {currentAlarmCondition ?? "NORMAL"} (Status={currentAlarmStatus})");

                // ✅ Ambil status SEBELUMNYA dari dictionary
                int? previousStatus = _previousAlarmStatus.GetValueOrDefault(liveData.Tank_Number);
                string previousCondition = _previousAlarmCondition.GetValueOrDefault(liveData.Tank_Number);

                Log($"  Previous Alarm: {previousCondition ?? "NORMAL"} (Status={previousStatus}), AckFromDB={ackFromDb}");

                // ========================================================
                // ✅ SIMPLIFIED ALARM LOGIC (FIX UNTUK SEMUA ALARM TYPE)
                // ========================================================
                if (currentAlarmCondition != null)
                {
                    // ========================================
                    // CASE 1: ALARM BARU (TRANSISI DARI NORMAL → ALARM)
                    // ========================================
                    if (previousStatus == null)
                    {
                        // ✅ Alarm pertama kali terdeteksi
                        liveData.Ack = true;  // BERBUNYI!

                        _logger.LogInformation("[ALARM-NEW] Tank={0} - NEW {1} alarm! Level={2}mm",
                            liveData.Tank_Number, currentAlarmCondition, currentLevel);
                        Log($"[CASE 1] NEW ALARM: {currentAlarmCondition} → Ack=TRUE (WILL SOUND) ✅");
                    }
                    // ========================================
                    // CASE 2: ALARM BERUBAH JENIS (HH↔H atau LL↔L atau antar kategori)
                    // ========================================
                    else if (previousStatus != currentAlarmStatus)
                    {
                        // ✅ Jenis alarm berubah → SELALU BERBUNYI (baik upgrade maupun downgrade)
                        liveData.Ack = true;  // BERBUNYI!

                        _logger.LogInformation("[ALARM-CHANGE] Tank={0} - Alarm changed: {1} → {2}! Level={3}mm",
                            liveData.Tank_Number,
                            GetAlarmTypeName(previousStatus),
                            currentAlarmCondition,
                            currentLevel);
                        Log($"[CASE 2] ALARM CHANGED: {GetAlarmTypeName(previousStatus)} → {currentAlarmCondition} → Ack=TRUE (WILL SOUND) ✅");
                    }
                    // ========================================
                    // CASE 3: ALARM SAMA (STATUS TIDAK BERUBAH)
                    // ========================================
                    else // previousStatus == currentAlarmStatus
                    {
                        Log($"[CASE 3] SAME ALARM: {currentAlarmCondition}");

                        // ✅ Cek apakah sudah pernah di-ACK
                        if (ackFromDb)
                        {
                            // ⚠️ BELUM DI-ACK → Tetap berbunyi sampai user ACK
                            liveData.Ack = true;  // TETAP BERBUNYI!
                            Log($"  → Not yet ACKed → Ack=TRUE (CONTINUE SOUND) ✅");
                        }
                        else
                        {
                            // ✅ SUDAH DI-ACK → Cek apakah perlu re-trigger

                            // Identifikasi jenis alarm
                            bool isCriticalAlarm = (currentAlarmStatus == 4 || currentAlarmStatus == 1); // HH atau LL

                            if (isCriticalAlarm && hasLiquidLevelChanged)
                            {
                                // ✅ CRITICAL ALARM (HH/LL) + Level berubah → BERBUNYI LAGI!
                                liveData.Ack = true;
                                _logger.LogInformation("[ALARM-RETRIGGER] Tank={0} - {1} re-triggered! Level: {2} → {3}mm",
                                    liveData.Tank_Number, currentAlarmCondition, lastLevel, currentLevel);
                                Log($"  → Already ACKed BUT level changed ({lastLevel}→{currentLevel}) → Ack=TRUE (RE-TRIGGER SOUND) ✅");
                            }
                            else
                            {
                                // ⏸️ WARNING ALARM (H/L) ATAU level stabil → TETAP SENYAP
                                liveData.Ack = false;
                                _logger.LogInformation("[ALARM-SILENT] Tank={0} - {1} acknowledged, level stable",
                                    liveData.Tank_Number, currentAlarmCondition);
                                Log($"  → Already ACKed, level stable → Ack=FALSE (SILENT) ⏸️");
                            }
                        }
                    }

                    // ✅ Set alarm message
                    SetAlarmMessage(liveData, currentAlarmCondition, tank);
                }
                else
                {
                    // ========================================
                    // CASE 4: ALARM HILANG (KEMBALI NORMAL)
                    // ========================================
                    if (previousStatus != null && previousStatus > 0)
                    {
                        _logger.LogInformation("[ALARM-CLEARED] Tank={0} - {1} cleared! Level={2}mm back to NORMAL",
                            liveData.Tank_Number, GetAlarmTypeName(previousStatus), currentLevel);
                        Log($"[CASE 4] ALARM CLEARED: {GetAlarmTypeName(previousStatus)} → NORMAL ✅");
                    }

                    ResetAlarmStatus(liveData);
                }

                // ✅ SIMPAN STATUS SAAT INI untuk cycle berikutnya
                _previousAlarmStatus[liveData.Tank_Number] = currentAlarmStatus;
                _previousAlarmCondition[liveData.Tank_Number] = currentAlarmCondition;

                Log($"[ALARM-RESULT] Tank={liveData.Tank_Number}, Status={liveData.Alarm_Status}, Ack={liveData.Ack}, Message={liveData.AlarmMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing alarm for {liveData.Tank_Number}");
                ResetAlarmStatus(liveData);
            }
        }

        // =========================================================
        // ALARM HELPER METHODS
        // =========================================================

        // Method helper untuk set alarm message
        private void SetAlarmMessage(TankLiveData liveData, string alarmCondition, Tank tank)
        {
            // Convert string alarm condition ke int untuk database
            liveData.Alarm_Status = ConvertAlarmStringToInt(alarmCondition);
            liveData.AlarmMessage = (liveData.Ack ?? true)
                ? $"Alarm status {alarmCondition} On {tank.Tank_Name} !!!"
                : "Alarm acknowledged - monitoring for changes";
        }

        // Method helper untuk reset alarm status (seperti kode referensi)
        private void ResetAlarmStatus(TankLiveData liveData)
        {
            liveData.Alarm_Status = null; // NULL = Normal
            liveData.AlarmMessage = null;
            liveData.Ack = true; // Reset Ack ke true (ready state)
        }

        // Helper untuk convert alarm string ke int (sesuai database server)
        private int ConvertAlarmStringToInt(string alarmCondition)
        {
            return alarmCondition switch
            {
                "LL" => 1,
                "L" => 2,
                "H" => 3,
                "HH" => 4,
                "TempL" => 5,
                "TempH" => 6,
                _ => 0  // Normal/Unknown
            };
        }

        private string GetAlarmTypeName(int? alarmStatus)
        {
            return (alarmStatus ?? 0) switch
            {
                1 => "LL",
                2 => "L",
                3 => "H",
                4 => "HH",
                5 => "TempL",
                6 => "TempH",
                _ => null  // Normal or unknown
            };
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker stopping...");
            Log("==============================================");
            Log($"TMS.ATGService STOPPED - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log("==============================================");

            _httpClient.Dispose();
            _fileLogger?.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }

    // =========================================================
    // ✅ HELPER CLASS untuk menyimpan state HOLD
    // =========================================================
    class TankFlowState
    {
        public int HoldRemaining { get; set; }
        public double? LastValidFlowrate { get; set; }
        public int ZeroCount { get; set; }
        public string ZeroReason { get; set; }
        public DateTime? FirstZeroTime { get; set; }
        public double? LastValidLevel { get; set; }
        public bool RetryAttempted { get; set; }
        public bool RetrySuccess { get; set; }
        public int RetryAttemptCount { get; set; }
        public int RateOfChangeViolationCount { get; set; }
        public int CrossRefViolationCount { get; set; }

        // ✅ NEW: Properties untuk flowrate baseline calculation
        // Karena TankLiveData.LastLiquidLevel dan LastTimeStamp adalah [NotMapped],
        // kita simpan di memory untuk persistence antar cycle
        public double? BaselineLiquidLevel { get; set; }
        public DateTime? BaselineTimeStamp { get; set; }
        public double? BaselineVolume { get; set; }
        public bool IsBaselineInitialized { get; set; } = false;
    }
}