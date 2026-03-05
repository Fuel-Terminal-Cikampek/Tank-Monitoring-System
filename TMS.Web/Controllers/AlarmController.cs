using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TMS.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Authorization;
using TMS.Web.Models;

namespace TMS.Web.Controllers
{
    [Authorize]
    public class AlarmController : Controller
    {
        class Alarm
        {
            public string TankName { get; set; }
            public string Type { get; set; }
            public string Message { get; set; }
        }

        private readonly TMSContext _context;
        public IConfiguration _configuration { get; }

        public AlarmController(TMSContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ========================================
        // ✅ HELPER: Check apakah Simulator diaktifkan
        // ========================================
        private bool IsSimulatorEnabled()
        {
            return _configuration.GetValue<bool>("AlarmSimulator:Enabled", false);
        }

        // ========================================
        // ✅ HELPER: Check apakah user adalah Admin
        // ========================================
        private bool IsUserAdmin()
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return RoleConstants.IsAdminRole(role);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult LoadData()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;

                var alarmData = (from tld in _context.Tank_Live_Data
                                 join t in _context.Tank on tld.Tank_Number equals t.Tank_Name
                                 join p in _context.Master_Product on t.Product_ID equals p.Product_Code
                                 where (tld.Alarm_Status != null && tld.Alarm_Status > 0 && !string.IsNullOrEmpty(tld.AlarmMessage))
                                 select new
                                 {
                                     Tank_Number = tld.Tank_Number,
                                     Tank_Name = t.Tank_Name,
                                     Product_Name = p.Product_Name,
                                     Level = tld.Level,
                                     Alarm_Status = tld.Alarm_Status,
                                     AlarmMessage = tld.AlarmMessage,
                                     Ack = tld.Ack,
                                     TimeStamp = tld.TimeStamp
                                 })
                                 .ToList();

                var dataWithString = alarmData.Select(item => new
                {
                    item.Tank_Number,
                    item.Tank_Name,
                    item.Product_Name,
                    item.Level,
                    AlarmStatus = GetAlarmTypeName(item.Alarm_Status),
                    item.AlarmMessage,
                    item.Ack,
                    item.TimeStamp
                }).ToList();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    dataWithString = dataWithString.Where(m => m.Tank_Name.Contains(searchValue) ||
                                                     m.AlarmMessage.Contains(searchValue)).ToList();
                }

                recordsTotal = dataWithString.Count();
                var data = dataWithString.OrderByDescending(a => a.TimeStamp).Skip(skip).Take(pageSize).ToList();

                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data },
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = null
                    });
            }
            catch (Exception)
            {
                throw;
            }
        }

        //[HttpGet]
        //public IActionResult TriggerAlarm()
        //{
        //    // ✅ FIX: Filter HANYA alarm yang belum di-ACK (Ack = true)
        //    // ✅ Use Tank_LiveDataTMS karena it has Ack and AlarmMessage columns
        //    List<TankLiveData> tankLiveData = _context.Tank_LiveData
        //        .Where(t => t.Alarm_Status != null && t.Alarm_Status > 0 && t.Ack == true)
        //        .ToList();

        //    List<Tank> tanks = _context.Tank.ToList();
        //    List<Alarm> alarms = new List<Alarm>();

        //    foreach (var liveData in tankLiveData)
        //    {
        //        var tank = tanks.FirstOrDefault(t => t.Tank_Name == liveData.Tank_Number);
        //        if (tank == null) continue;

        //        if (liveData.Alarm_Status.HasValue && liveData.Alarm_Status.Value > 0 && !string.IsNullOrEmpty(liveData.AlarmMessage))
        //        {
        //            alarms.Add(new Alarm
        //            {
        //                TankName = tank.Tank_Name,
        //                Type = GetAlarmTypeName(liveData.Alarm_Status),
        //                Message = liveData.AlarmMessage
        //            });
        //        }
        //    }

        //    if (alarms.Count > 0)
        //    {
        //        return Json(new { isValid = true, data = alarms });
        //    }
        //    else
        //    {
        //        return Json(new { isValid = false, data = alarms });
        //    }
        //}

        // ========================================
        // ✅ FIXED: AckAlarm (untuk tombol ACK lama)
        // Sekarang update KEDUA tabel
        // ========================================
        [HttpGet]
        public IActionResult AckAlarm(string tankName = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(tankName))
                {
                    // ✅ FIX: Update KEDUA tabel
                    
                    // 1. Tank_Live_Data
                    var liveData = _context.Tank_Live_Data.FirstOrDefault(t => t.Tank_Number == tankName);
                    if (liveData != null)
                    {
                        liveData.Ack = false;
                    }

                    // 2. Tank_LiveDataTMS
                    var liveDataTMS = _context.Tank_LiveDataTMS.FirstOrDefault(t => t.Tank_Number == tankName);
                    if (liveDataTMS != null)
                    {
                        liveDataTMS.Ack = false;
                    }

                    _context.SaveChanges();
                }
                else
                {
                    // ACK ALL
                    
                    // 1. Tank_Live_Data
                    var activeAlarmsMain = _context.Tank_Live_Data
                        .Where(t => t.Ack == true)
                        .ToList();

                    foreach (var alarm in activeAlarmsMain)
                    {
                        alarm.Ack = false;
                    }

                    // 2. Tank_LiveDataTMS
                    var activeAlarmsTMS = _context.Tank_LiveDataTMS
                        .Where(t => t.Ack == true)
                        .ToList();

                    foreach (var alarm in activeAlarmsTMS)
                    {
                        alarm.Ack = false;
                    }

                    _context.SaveChanges();
                }

                return Json(new { isValid = true, message = "Success to Ack alarm" });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        // ========================================
        // ✅ SIMULASI ALARM (PROTECTED dengan Feature Flag + Admin-Only)
        // ========================================
        [HttpPost]
        public IActionResult SimulateAlarm(string tankNumber, int levelValue)
        {
            // ✅ CHECK #1: Feature Flag
            if (!IsSimulatorEnabled())
            {
                return Json(new { isValid = false, message = "❌ Alarm Simulator is DISABLED in this environment" });
            }

            // ✅ CHECK #2: Admin only
            if (!IsUserAdmin())
            {
                return Json(new { isValid = false, message = "❌ Only Admin users can simulate alarms" });
            }

            try
            {
                if (string.IsNullOrEmpty(tankNumber) || levelValue < 0)
                {
                    return Json(new { isValid = false, message = "tankNumber dan levelValue harus valid" });
                }

                var tankLiveData = _context.Tank_Live_Data.FirstOrDefault(t => t.Tank_Number == tankNumber);
                if (tankLiveData == null)
                {
                    return Json(new { isValid = false, message = $"Tank {tankNumber} tidak ditemukan" });
                }

                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankNumber);
                if (tank == null)
                {
                    return Json(new { isValid = false, message = $"Konfigurasi tank {tankNumber} tidak ditemukan" });
                }

                tankLiveData.Level = levelValue;
                tankLiveData.TimeStamp = DateTime.Now;

                double currentLevel = levelValue;
                var debugInfo = new List<string>();

                debugInfo.Add($"[DEBUG] Tank={tankNumber}, Level={levelValue}mm");
                debugInfo.Add($"[DEBUG] Thresholds: LL={tank.LevelLL}mm (use={tank.UseAlarmLL}), L={tank.LevelL}mm (use={tank.UseAlarmL}), H={tank.LevelH}mm (use={tank.UseAlarmH}), HH={tank.LevelHH}mm (use={tank.UseAlarmHH})");

                // ✅ Definisi alarm levels dengan NULL SAFETY - SAMA dengan WorkerXmlMode
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

                // ✅ Cari alarm level condition yang SEDANG AKTIF (prioritas: HH > H > L > LL)
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

                string currentAlarmCondition = levelAlarm.Key;
                int? currentAlarmStatus = currentAlarmCondition != null ? ConvertAlarmStringToInt(currentAlarmCondition) : (int?)null;

                debugInfo.Add($"[DEBUG] Detected Alarm: {currentAlarmCondition ?? "NORMAL"} (Status={currentAlarmStatus})");

                // ✅ Ambil status alarm SEBELUMNYA dari database (SAMA dengan WorkerXmlMode)
                // ✅ Use Tank_LiveDataTMS because it has Ack column
                var existingLiveData = _context.Tank_LiveDataTMS.AsNoTracking().FirstOrDefault(t => t.Tank_Number == tankNumber);
                int? previousStatus = existingLiveData?.Alarm_Status;
                bool ackFromDb = existingLiveData?.Ack ?? true;
                double lastLevel = existingLiveData?.Level ?? 0;
                bool hasLiquidLevelChanged = Math.Abs(currentLevel - lastLevel) > 1.0;

                debugInfo.Add($"[DEBUG] Previous: Status={GetAlarmTypeName(previousStatus)}, Ack={ackFromDb}, LastLevel={lastLevel}mm, LevelChanged={hasLiquidLevelChanged}");

                // ========================================================
                // ✅ SIMPLIFIED ALARM LOGIC (SAMA PERSIS dengan WorkerXmlMode)
                // ========================================================
                if (currentAlarmCondition != null)
                {
                    // ========================================
                    // CASE 1: ALARM BARU (TRANSISI DARI NORMAL → ALARM)
                    // ========================================
                    if (previousStatus == null)
                    {
                        // ✅ Alarm pertama kali terdeteksi
                        tankLiveData.Ack = true;  // BERBUNYI!
                        debugInfo.Add($"[CASE 1] NEW ALARM: {currentAlarmCondition} → Ack=TRUE (WILL SOUND) ✅");
                    }
                    // ========================================
                    // CASE 2: ALARM BERUBAH JENIS (HH↔H atau LL↔L atau antar kategori)
                    // ========================================
                    else if (previousStatus != currentAlarmStatus)
                    {
                        // ✅ Jenis alarm berubah → SELALU BERBUNYI
                        tankLiveData.Ack = true;  // BERBUNYI!
                        debugInfo.Add($"[CASE 2] ALARM CHANGED: {GetAlarmTypeName(previousStatus)} → {currentAlarmCondition} → Ack=TRUE (WILL SOUND) ✅");
                    }
                    // ========================================
                    // CASE 3: ALARM SAMA (STATUS TIDAK BERUBAH)
                    // ========================================
                    else // previousStatus == currentAlarmStatus
                    {
                        debugInfo.Add($"[CASE 3] SAME ALARM: {currentAlarmCondition}");

                        // ✅ Cek apakah sudah pernah di-ACK
                        if (ackFromDb)
                        {
                            // ⚠️ BELUM DI-ACK → Tetap berbunyi sampai user ACK
                            tankLiveData.Ack = true;  // TETAP BERBUNYI!
                            debugInfo.Add($"  → Not yet ACKed → Ack=TRUE (CONTINUE SOUND) ✅");
                        }
                        else
                        {
                            // ✅ SUDAH DI-ACK → Cek apakah perlu re-trigger

                            // Identifikasi jenis alarm
                            bool isCriticalAlarm = (currentAlarmStatus == 4 || currentAlarmStatus == 1); // HH atau LL

                            if (isCriticalAlarm && hasLiquidLevelChanged)
                            {
                                // ✅ CRITICAL ALARM (HH/LL) + Level berubah → BERBUNYI LAGI!
                                tankLiveData.Ack = true;
                                debugInfo.Add($"  → Already ACKed BUT level changed ({lastLevel}→{currentLevel}) → Ack=TRUE (RE-TRIGGER SOUND) ✅");
                            }
                            else
                            {
                                // ⏸️ WARNING ALARM (H/L) ATAU level stabil → TETAP SENYAP
                                tankLiveData.Ack = false;
                                debugInfo.Add($"  → Already ACKed, level stable → Ack=FALSE (SILENT) ⏸️");
                            }
                        }
                    }

                    tankLiveData.Alarm_Status = currentAlarmStatus;
                    tankLiveData.AlarmMessage = $"[SIMULASI] Alarm {currentAlarmCondition} pada {tank.Tank_Name}! Level: {levelValue}mm";
                    debugInfo.Add($"✅ DB UPDATE: Status={currentAlarmStatus}, Ack={tankLiveData.Ack}, Message set");
                }
                else
                {
                    // ========================================
                    // CASE 4: ALARM HILANG (KEMBALI NORMAL)
                    // ========================================
                    if (previousStatus != null && previousStatus > 0)
                    {
                        debugInfo.Add($"[CASE 4] ALARM CLEARED: {GetAlarmTypeName(previousStatus)} → NORMAL ✅");
                    }

                    tankLiveData.Alarm_Status = null;
                    tankLiveData.AlarmMessage = null;
                    tankLiveData.Ack = true;  // Reset ke ready state
                    debugInfo.Add($"✅ DB UPDATE: Alarm reset to NORMAL");
                }

                _context.SaveChanges();
                debugInfo.Add($"✅ Database saved successfully");

                return Json(new
                {
                    isValid = true,
                    message = $"Simulasi: {tankNumber} level={levelValue}mm",
                    tankNumber = tankNumber,
                    level = levelValue,
                    alarmCondition = currentAlarmCondition,
                    alarmStatus = currentAlarmStatus,
                    ack = tankLiveData.Ack,
                    previousStatus = GetAlarmTypeName(previousStatus),
                    thresholds = new
                    {
                        LL = tank.LevelLL,
                        L = tank.LevelL,
                        H = tank.LevelH,
                        HH = tank.LevelHH
                    },
                    debug = string.Join("\n", debugInfo)
                });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = $"Error: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public IActionResult GetTankAlarmConfig()
        {
            // ✅ CHECK: Feature Flag
            if (!IsSimulatorEnabled())
            {
                return Json(new { isValid = false, message = "❌ Alarm Simulator is DISABLED" });
            }

            // ✅ CHECK: Admin only
            if (!IsUserAdmin())
            {
                return Json(new { isValid = false, message = "❌ Only Admin users can access simulator config" });
            }

            try
            {
                var tanks = _context.Tank
                    .Select(t => new
                    {
                        tankName = t.Tank_Name,
                        product = t.Product_ID,
                        levelLL = t.LevelLL,
                        useAlarmLL = t.UseAlarmLL,
                        levelL = t.LevelL,
                        useAlarmL = t.UseAlarmL,
                        levelH = t.LevelH,
                        useAlarmH = t.UseAlarmH,
                        levelHH = t.LevelHH,
                        useAlarmHH = t.UseAlarmHH
                    })
                    .ToList();

                return Json(new { isValid = true, data = tanks });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ResetAlarm(string tankNumber)
        {
            // ✅ CHECK: Feature Flag
            if (!IsSimulatorEnabled())
            {
                return Json(new { isValid = false, message = "❌ Alarm Simulator is DISABLED" });
            }

            // ✅ CHECK: Admin only
            if (!IsUserAdmin())
            {
                return Json(new { isValid = false, message = "❌ Only Admin users can reset alarms" });
            }

            try
            {
                var tankLiveData = _context.Tank_Live_Data.FirstOrDefault(t => t.Tank_Number == tankNumber);
                if (tankLiveData == null)
                {
                    return Json(new { isValid = false, message = $"Tank {tankNumber} tidak ditemukan" });
                }

                tankLiveData.Level = 0;
                tankLiveData.Alarm_Status = null;
                tankLiveData.AlarmMessage = null;
                tankLiveData.Ack = false;
                tankLiveData.TimeStamp = DateTime.Now;

                _context.SaveChanges();

                return Json(new
                {
                    isValid = true,
                    message = $"Reset alarm {tankNumber} berhasil",
                    tankNumber = tankNumber
                });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        // ========================================
        // ✅ SIMULATOR PAGE (PROTECTED)
        // ========================================
        public IActionResult Simulator()
        {
            // ✅ CHECK: Feature Flag
            if (!IsSimulatorEnabled())
            {
                return NotFound();  // Simulasi tidak ada jika disabled
            }

            // ✅ CHECK: Admin only
            if (!IsUserAdmin())
            {
                return Unauthorized();  // Redirect ke login jika bukan admin
            }

            return View();
        }

        // Helper untuk convert alarm string ke int (sesuai WorkerXmlMode)
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
                _ => null
            };
        }

        /// <summary>
        /// ✅ NEW: Check if alarm is valid based on Tank Configuration
        /// </summary>
        private bool IsAlarmValidForTank(Tank tank, int? alarmStatus)
        {
            if (tank == null || alarmStatus == null || alarmStatus <= 0) return false;

            return alarmStatus switch
            {
                1 => tank.UseAlarmLL ?? false,  // LL
                2 => tank.UseAlarmL ?? false,   // L
                3 => tank.UseAlarmH ?? false,   // H
                4 => tank.UseAlarmHH ?? false,  // HH
                5 => tank.UseAlarmTempL ?? false, // TempL
                6 => tank.UseAlarmTempH ?? false, // TempH
                _ => false
            };
        }

        // ========================================
        // ✅ FIXED: Get ALL Active Alarms - Now validates against Tank Configuration
        // ========================================
        [HttpGet]
        public IActionResult GetAllActiveAlarms()
        {
            try
            {
                const double FLOWRATE_TOLERANCE = 0.05;
                const int LEVEL_FLUCTUATION_MM = 2;

                CheckAndUpdateLevelAlarms();
                CheckAndUpdateOperationAlarms();

                var alarms = new List<object>();
                var alarmHistory = new List<object>();

                // ✅ Load Tank Configuration for validation
                var tankConfigs = _context.Tank.ToList();

                // ========================================
                // 1. Level Alarms - VALIDATE against Tank Configuration
                // ========================================
                var activeLevelAlarms = _context.Tank_LiveDataTMS
                    .Where(t => t.Alarm_Status != null && t.Alarm_Status > 0 && t.Ack == true)
                    .Select(t => new
                    {
                        t.Tank_Number,
                        t.Alarm_Status,
                        t.AlarmMessage,
                        t.Level,
                        t.TimeStamp
                    })
                    .ToList();

                foreach (var alarm in activeLevelAlarms)
                {
                    // ✅ VALIDATE: Check if UseAlarm is enabled for this alarm type
                    var tankConfig = tankConfigs.FirstOrDefault(t => t.Tank_Name == alarm.Tank_Number);
                    if (tankConfig == null) continue;

                    // Skip if alarm is not enabled in Tank Configuration
                    if (!IsAlarmValidForTank(tankConfig, alarm.Alarm_Status))
                    {
                        Console.WriteLine($"[GetAllActiveAlarms] SKIPPED: Tank={alarm.Tank_Number}, AlarmStatus={alarm.Alarm_Status} - Alarm disabled in config");
                        continue;
                    }

                    string alarmType = GetAlarmTypeName(alarm.Alarm_Status);
                    bool isCritical = alarm.Alarm_Status == 1 || alarm.Alarm_Status == 4 ||
                                      alarm.Alarm_Status == 5 || alarm.Alarm_Status == 6;
                    string alarmCategory = (alarm.Alarm_Status == 5 || alarm.Alarm_Status == 6)
                        ? "Temperature" : "Level";

                    alarms.Add(new
                    {
                        id = $"level_{alarm.Tank_Number}",
                        tankNumber = alarm.Tank_Number,
                        alarmCategory = alarmCategory,
                        alarmType = alarmType ?? "Unknown",
                        message = alarm.AlarmMessage ?? $"{alarmCategory} {alarmType} pada {alarm.Tank_Number}",
                        level = alarm.Level,
                        isCritical = isCritical,
                        isActive = true,
                        isAcknowledged = false,
                        timestamp = alarm.TimeStamp,
                        color = isCritical ? "danger" : "warning"
                    });
                }

                // ========================================
                // 2. Stagnant/No Flowrate Alarms
                // ========================================
                var activeStagnantAlarms = _context.Tank_Movement
                    .Where(m => m.Status == 1 && m.StagnantAlarm == true)
                    .Select(m => new
                    {
                        m.Tank_Number,
                        m.Tank_Movement_ID,
                        m.LastFlowrateChangeTime,
                        m.AlarmTimeStamp,
                        m.StagnantThresholdMinutes,
                        m.Level
                    })
                    .ToList();

                foreach (var alarm in activeStagnantAlarms)
                {
                    DateTime baselineTime = alarm.LastFlowrateChangeTime ?? DateTime.Now;
                    if (alarm.AlarmTimeStamp.HasValue)
                    {
                        DateTime alarmDateTime = DateTime.Today.Add(alarm.AlarmTimeStamp.Value);
                        if (alarmDateTime > DateTime.Now) alarmDateTime = alarmDateTime.AddDays(-1);
                        if (alarmDateTime > baselineTime) baselineTime = alarmDateTime;
                    }
                    var duration = DateTime.Now - baselineTime;

                    alarms.Add(new
                    {
                        id = $"stagnant_{alarm.Tank_Number}",
                        tankNumber = alarm.Tank_Number,
                        alarmCategory = "Stagnant",
                        alarmType = "No Flowrate",
                        message = $"Tank {alarm.Tank_Number} tidak ada flowrate selama {Math.Round(duration.TotalMinutes, 0)} menit",
                        level = alarm.Level,
                        isCritical = true,
                        isActive = true,
                        isAcknowledged = false,
                        timestamp = baselineTime,
                        durationMinutes = Math.Round(duration.TotalMinutes, 1),
                        thresholdMinutes = alarm.StagnantThresholdMinutes ?? 5,
                        color = "danger"
                    });
                }

                // ========================================
                // 3. Operation Type Alarms
                // ========================================
                var liveDataList = _context.Tank_Live_Data.ToList();

                var operationMovements = _context.Tank_Movement
                    .Where(m => m.Status != null && m.OperationAlarmAck == false && m.DisableOperationAlarm == false)
                    .ToList();

                double FLOWRATE_TOLERANCE_STANDBY = _configuration.GetValue<double>("AlarmSettings:StandbyFlowrateToleranceKLH", 20.0);
                int STANDBY_LEVEL_CHANGE_TOLERANCE = _configuration.GetValue<int>("AlarmSettings:StandbyLevelChangeToleranceMM", 10);
                double FLOWRATE_TOLERANCE_SALES = _configuration.GetValue<double>("AlarmSettings:SalesFlowrateToleranceKLH", 20.0);
                int SALES_LEVEL_CHANGE_TOLERANCE = _configuration.GetValue<int>("AlarmSettings:SalesLevelChangeToleranceMM", 5);

                foreach (var tm in operationMovements)
                {
                    // ✅ DOUBLE CHECK: Skip jika DisableOperationAlarm = true (untuk safety)
                    if (tm.DisableOperationAlarm)
                    {
                        Console.WriteLine($"[GetAllActiveAlarms] SKIPPED Tank={tm.Tank_Number} (DisableOperationAlarm=true)");
                        continue;
                    }

                    var tld = liveDataList.FirstOrDefault(ld => ld.Tank_Number == tm.Tank_Number);
                    if (tld == null) continue;

                    bool shouldAlarm = false;
                    string alarmType = "";
                    string operationType = "";
                    string message = "";

                    int currentLevel = (int)(tld.Level ?? 0);
                    int previousLevel = tm.Level ?? currentLevel;
                    int levelDelta = currentLevel - previousLevel;
                    int absLevelDelta = Math.Abs(levelDelta);
                    double currentFlowrate = tld.Flowrate ?? 0;
                    double absFlowrate = Math.Abs(currentFlowrate);

                    // ========================================
                    // RECEIVING (Status = 1)
                    // ========================================
                    if (tm.Status == 1)
                    {
                        // ✅ Kondisi 1: Target tercapai
                        if (tm.TargetLevel > 0 && currentLevel >= tm.TargetLevel)
                        {
                            shouldAlarm = true;
                            alarmType = "Receiving Target";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} sudah mencapai target level {tm.TargetLevel}mm (current: {currentLevel}mm)";
                        }
                        // ✅ Kondisi 2: Flowrate NEGATIF (flow keluar saat seharusnya masuk)
                        else if (currentFlowrate <= -FLOWRATE_TOLERANCE_STANDBY)
                        {
                            shouldAlarm = true;
                            alarmType = "Receiving Flowrate Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam RECEIVING tapi ada flowrate NEGATIF: {currentFlowrate:F1} KL/h";
                        }
                        // ✅ Kondisi 3: Level TURUN signifikan (anomaly)
                        else if (levelDelta <= -STANDBY_LEVEL_CHANGE_TOLERANCE)
                        {
                            shouldAlarm = true;
                            alarmType = "Receiving Level Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam RECEIVING tapi level TURUN: {previousLevel}→{currentLevel}mm ({levelDelta:+#;-#;0}mm)";
                        }
                    }
                    // ========================================
                    // SALES (Status = 2)
                    // ========================================
                    else if (tm.Status == 2)
                    {
                        // Target tercapai
                        if (tm.TargetLevel > 0 && currentLevel <= tm.TargetLevel)
                        {
                            shouldAlarm = true;
                            alarmType = "Sales Low";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} level rendah mencapai {currentLevel}mm (target: {tm.TargetLevel}mm)";
                        }
                        // Flowrate POSITIF (naik)
                        else if (currentFlowrate >= FLOWRATE_TOLERANCE_SALES)
                        {
                            shouldAlarm = true;
                            alarmType = "Sales Flowrate Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam SALES tapi ada flowrate NAIK: +{currentFlowrate:F1} KL/h (threshold: {FLOWRATE_TOLERANCE_SALES} KL/h)";
                        }
                        // Level NAIK
                        else if (levelDelta >= SALES_LEVEL_CHANGE_TOLERANCE)
                        {
                            shouldAlarm = true;
                            alarmType = "Sales Level Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam SALES tapi level NAIK: {previousLevel}→{currentLevel}mm (+{levelDelta}mm, threshold: {SALES_LEVEL_CHANGE_TOLERANCE}mm)";
                        }
                    }
                    // ========================================
                    // ✅ STANDBY (Status = 0) - 2 KONDISI
                    // ========================================
                    else if (tm.Status == 0)
                    {
                        // Kondisi 1: Flowrate anomaly
                        if (absFlowrate >= FLOWRATE_TOLERANCE_STANDBY)
                        {
                            shouldAlarm = true;
                            alarmType = "Standby Flowrate Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam STANDBY tapi ada flowrate: {currentFlowrate:F1} KL/h (threshold: ±{FLOWRATE_TOLERANCE_STANDBY} KL/h)";
                        }
                        // Kondisi 2: Level change anomaly (KEDUA ARAH)
                        else if (absLevelDelta >= STANDBY_LEVEL_CHANGE_TOLERANCE)
                        {
                            string direction = levelDelta > 0 ? "NAIK" : "TURUN";
                            shouldAlarm = true;
                            alarmType = "Standby Level Anomaly";
                            operationType = "Operation";
                            message = $"Tank {tm.Tank_Number} dalam STANDBY tapi level {direction}: {previousLevel}→{currentLevel}mm ({levelDelta:+#;-#;0}mm, threshold: ±{STANDBY_LEVEL_CHANGE_TOLERANCE}mm)";
                        }
                    }

                    if (shouldAlarm)
                    {
                        // ✅ VALIDATE: Check if UseAlarm is enabled for this alarm type
                        var tankConfig = tankConfigs.FirstOrDefault(t => t.Tank_Name == tm.Tank_Number);
                        if (tankConfig == null) continue;

                        // Skip if alarm is not enabled in Tank Configuration
                        if (!IsAlarmValidForTank(tankConfig, null))
                        {
                            Console.WriteLine($"[GetAllActiveAlarms] SKIPPED: Tank={tm.Tank_Number}, Status={tm.Status} - Alarm disabled in config");
                            continue;
                        }

                        alarms.Add(new
                        {
                            id = $"operation_{tm.Tank_Number}_{tm.Status}",
                            tankNumber = tm.Tank_Number,
                            alarmCategory = operationType,
                            alarmType = alarmType,
                            message = message,
                            level = currentLevel,
                            previousLevel = previousLevel,
                            levelDelta = levelDelta,
                            flowrate = currentFlowrate,
                            isCritical = true,
                            isActive = true,
                            isAcknowledged = false,
                            timestamp = DateTime.Now,
                            color = "danger"
                        });
                    }
                }

                // ========================================
                // 4. Acknowledged alarms (history) - ALSO VALIDATE
                // ========================================
                var acknowledgedLevelAlarms = _context.Tank_LiveDataTMS
                    .Where(t => t.Alarm_Status != null && t.Alarm_Status > 0 && t.Ack == false)
                    .Select(t => new { t.Tank_Number, t.Alarm_Status, t.AlarmMessage, t.Level, t.TimeStamp })
                    .ToList();

                foreach (var alarm in acknowledgedLevelAlarms)
                {
                    // ✅ VALIDATE: Check if UseAlarm is enabled
                    var tankConfig = tankConfigs.FirstOrDefault(t => t.Tank_Name == alarm.Tank_Number);
                    if (tankConfig == null) continue;
                    if (!IsAlarmValidForTank(tankConfig, alarm.Alarm_Status)) continue;

                    string alarmType = GetAlarmTypeName(alarm.Alarm_Status);
                    bool isCritical = alarm.Alarm_Status == 1 || alarm.Alarm_Status == 4 ||
                                      alarm.Alarm_Status == 5 || alarm.Alarm_Status == 6;
                    string alarmCategory = (alarm.Alarm_Status == 5 || alarm.Alarm_Status == 6)
                        ? "Temperature" : "Level";

                    alarmHistory.Add(new
                    {
                        id = $"level_{alarm.Tank_Number}",
                        tankNumber = alarm.Tank_Number,
                        alarmCategory = alarmCategory,
                        alarmType = alarmType ?? "Unknown",
                        message = alarm.AlarmMessage ?? $"{alarmCategory} {alarmType} pada {alarm.Tank_Number}",
                        level = alarm.Level,
                        isCritical = isCritical,
                        isActive = false,
                        isAcknowledged = true,
                        timestamp = alarm.TimeStamp,
                        color = "secondary"
                    });
                }

                var sortedAlarms = alarms
                    .OrderByDescending(a => ((dynamic)a).isActive)
                    .ThenByDescending(a => ((dynamic)a).isCritical)
                    .ThenByDescending(a => ((dynamic)a).timestamp)
                    .ToList();

                var sortedHistory = alarmHistory
                    .OrderByDescending(a => ((dynamic)a).timestamp)
                    .ToList();

                var allForDisplay = sortedAlarms.Concat(sortedHistory).ToList();

                return Json(new
                {
                    isValid = alarms.Count > 0,
                    hasActiveAlarms = alarms.Count > 0,
                    totalActiveCount = alarms.Count,
                    totalAcknowledgedCount = alarmHistory.Count,
                    levelActiveCount = alarms.Count(a => ((dynamic)a).alarmCategory == "Level"),
                    stagnantActiveCount = activeStagnantAlarms.Count,
                    operationActiveCount = alarms.Count(a => ((dynamic)a).alarmCategory == "Operation"),
                    data = sortedAlarms,
                    history = sortedHistory,
                    allAlarms = allForDisplay
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllActiveAlarms] ERROR: {ex.Message}");
                return Json(new { isValid = false, message = ex.Message, data = new List<object>(), history = new List<object>() });
            }
        }

        // ========================================
        // ✅ UPDATED: ACK All Alarms - PROPER LOGIC
        // Set OperationAlarmAck = TRUE untuk mematikan alarm
        // Alarm TIDAK akan bunyi lagi sampai kondisi CLEAR dulu
        // ========================================
        [HttpPost]
        public IActionResult AckAllAlarms()
        {
            try
            {
                int levelAcked = 0;
                int stagnantAcked = 0;
                int operationAcked = 0;

                // ========================================
                // 1. ACK Level Alarms (Tank_LiveDataTMS)
                // ========================================
                var levelAlarmsTMS = _context.Tank_LiveDataTMS
                    .Where(t => t.Alarm_Status != null && t.Alarm_Status > 0 && t.Ack == true)
                    .ToList();

                foreach (var alarm in levelAlarmsTMS)
                {
                    alarm.Ack = false;  // false = sudah di-ACK (silent)
                    levelAcked++;
                }

                // ========================================
                // 2. ACK Stagnant Alarms
                // ========================================
                var stagnantAlarms = _context.Tank_Movement
                    .Where(m => m.Status == 1 && m.StagnantAlarm == true)
                    .ToList();

                foreach (var alarm in stagnantAlarms)
                {
                    alarm.StagnantAlarm = false;
                    alarm.AlarmTimeStamp = TimeSpan.FromHours(DateTime.Now.Hour)
                        .Add(TimeSpan.FromMinutes(DateTime.Now.Minute))
                        .Add(TimeSpan.FromSeconds(DateTime.Now.Second));
                    stagnantAcked++;
                }

                // ========================================
                // ✅ 3. ACK Operation Alarms - SET OperationAlarmAck = TRUE
                // Ini akan membuat alarm SILENT sampai kondisi CLEAR
                // ========================================
                var liveDataList = _context.Tank_Live_Data.ToList();
                double FLOWRATE_TOLERANCE_STANDBY = _configuration.GetValue<double>("AlarmSettings:StandbyFlowrateToleranceKLH", 20.0);
                double FLOWRATE_TOLERANCE_SALES = _configuration.GetValue<double>("AlarmSettings:SalesFlowrateToleranceKLH", 20.0);

                var operationAlarms = _context.Tank_Movement
                    .Where(m => m.Status != null && m.OperationAlarmAck == false)
                    .ToList();

                foreach (var tm in operationAlarms)
                {
                    var tld = liveDataList.FirstOrDefault(ld => ld.Tank_Number == tm.Tank_Number);
                    if (tld == null) continue;

                    bool alarmConditionMet = false;
                    int currentLevel = (int)(tld.Level ?? 0);
                    double currentFlowrate = tld.Flowrate ?? 0;
                    double absFlowrate = Math.Abs(currentFlowrate);

                    // Check if alarm condition is currently met
                    if (tm.Status == 1 && tm.TargetLevel > 0)
                    {
                        alarmConditionMet = currentLevel >= tm.TargetLevel;
                    }
                    else if (tm.Status == 2)
                    {
                        bool targetReached = tm.TargetLevel > 0 && currentLevel <= tm.TargetLevel;
                        bool hasPositiveFlowrate = currentFlowrate >= FLOWRATE_TOLERANCE_SALES;
                        alarmConditionMet = targetReached || hasPositiveFlowrate;
                    }
                    else if (tm.Status == 0)
                    {
                        alarmConditionMet = absFlowrate >= FLOWRATE_TOLERANCE_STANDBY;
                    }

                    // ✅ HANYA ACK jika kondisi alarm TERPENUHI
                    // Jika kondisi sudah clear, tidak perlu ACK
                    if (alarmConditionMet)
                    {
                        tm.OperationAlarmAck = true;  // TRUE = sudah di-ACK (SILENT)
                        operationAcked++;
                        
                        Console.WriteLine($"[AckAllAlarms] ACKed operation alarm: Tank={tm.Tank_Number}, Status={tm.Status}");
                    }
                }

                _context.SaveChanges();

                int totalAcked = levelAcked + stagnantAcked + operationAcked;

                if (totalAcked == 0)
                {
                    Console.WriteLine($"[AckAllAlarms] No active alarms to acknowledge");
                    return Json(new
                    {
                        isValid = true,
                        message = "Tidak ada alarm aktif",
                        levelAcked = 0,
                        stagnantAcked = 0,
                        operationAcked = 0,
                        timestamp = DateTime.Now
                    });
                }

                Console.WriteLine($"[AckAllAlarms] GLOBAL ACK: {levelAcked} level + {stagnantAcked} stagnant + {operationAcked} operation alarms acknowledged");

                return Json(new
                {
                    isValid = true,
                    message = $"✓ {totalAcked} alarm dimatikan",
                    levelAcked = levelAcked,
                    stagnantAcked = stagnantAcked,
                    operationAcked = operationAcked,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AckAllAlarms] ERROR: {ex.Message}");
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        // ========================================
        // ✅ NEW: Tank Operation Alarm System
        // ========================================
        
        /// <summary>
        /// Get active tank operation alarms (RECEIVING/SALES/STANDBY monitoring)
        /// Checks TankMovement status vs TankLiveData level/flowrate
        /// Auto-resets ACK flag when alarm condition clears
        /// </summary>
        [HttpGet]
        public IActionResult GetTankOperationAlarms()
        {
            try
            {
                var alarms = new List<object>();

                // Get all active tank movements (Status != null)
                var movements = _context.Tank_Movement
                    .Where(m => m.Status != null)
                    .ToList();

                var liveData = _context.Tank_Live_Data.ToList();
                bool hasChanges = false;

                foreach (var tm in movements)
                {
                    var tld = liveData.FirstOrDefault(ld => ld.Tank_Number == tm.Tank_Number);
                    if (tld == null) continue;

                    bool shouldAlarm = false;
                    string alarmType = "";
                    string operationType = "";
                    bool alarmConditionActive = false;

                    // ========================================
                    // Check RECEIVING (High Alarm)
                    // Trigger: Level >= TargetLevel
                    // ========================================
                    if (tm.Status == 1)  // RECEIVING
                    {
                        alarmConditionActive = (tld.Level ?? 0) >= (tm.TargetLevel ?? 0) && tm.TargetLevel > 0;
                        
                        if (alarmConditionActive)
                        {
                            shouldAlarm = true;
                            alarmType = "receiving_high";
                            operationType = "Receiving";
                        }
                    }
                    // ========================================
                    // Check SALES (Low Alarm)
                    // Trigger: Level <= TargetLevel
                    // ========================================
                    else if (tm.Status == 2)  // SALES
                    {
                        alarmConditionActive = (tld.Level ?? 0) <= (tm.TargetLevel ?? 0) && tm.TargetLevel > 0;
                        
                        if (alarmConditionActive)
                        {
                            shouldAlarm = true;
                            alarmType = "sales_low";
                            operationType = "Sales";
                        }
                    }
                    // ========================================
                    // Check STANDBY ANOMALY
                    // Trigger: Flowrate != 0
                    // ========================================
                    else if (tm.Status == 0)  // STANDBY
                    {
                        alarmConditionActive = Math.Abs(tld.Flowrate ?? 0) > 0.01;  // ✅ FIX: Use threshold
                        
                        if (alarmConditionActive)
                        {
                            shouldAlarm = true;
                            alarmType = "standby_anomaly";
                            operationType = "Standby Anomaly";
                        }
                    }

                    if (shouldAlarm)
                    {
                        alarms.Add(new
                        {
                            id = $"{tm.Tank_Number}_{tm.Status}",
                            tankNumber = tm.Tank_Number,
                            operationType = operationType,
                            alarmType = alarmType,
                            message = $"Alarm {tm.Tank_Number} {operationType}",
                            isAcknowledged = tm.OperationAlarmAck,
                            isConditionActive = alarmConditionActive,
                            level = tld.Level ?? 0,
                            targetLevel = tm.TargetLevel ?? 0,
                            flowrate = tld.Flowrate ?? 0
                        });
                    }
                }

                return Json(new
                {
                    isValid = alarms.Count > 0,
                    totalCount = alarms.Count,
                    unacknowledgedCount = alarms.Count(a => !(bool)((dynamic)a).isAcknowledged),
                    data = alarms
                }, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetTankOperationAlarms] ERROR: {ex.Message}");
                return Json(new { isValid = false, message = ex.Message, data = new List<object>() });
            }
        }

        /// <summary>
        /// Acknowledge individual tank operation alarm
        /// Sets OperationAlarmAck flag to true for specific tank+status combination
        /// </summary>
        [HttpPost]
        public IActionResult AckOperationAlarm(string alarmId)
        {
            try
            {
                if (string.IsNullOrEmpty(alarmId))
                    return Json(new { isValid = false, message = "Alarm ID required" });

                // Parse alarmId format: "T-01_1" (TankNumber_Status)
                var parts = alarmId.Split('_');
                if (parts.Length != 2)
                    return Json(new { isValid = false, message = "Invalid alarm ID format. Expected: TankNumber_Status" });

                string tankNumber = parts[0];
                if (!int.TryParse(parts[1], out int status))
                    return Json(new { isValid = false, message = "Invalid status value" });

                var movement = _context.Tank_Movement
                    .FirstOrDefault(m => m.Tank_Number == tankNumber && m.Status == status);

                if (movement == null)
                    return Json(new { isValid = false, message = $"Movement not found for tank {tankNumber} with status {status}" });

                movement.OperationAlarmAck = true;
                _context.SaveChanges();

                return Json(new
                {
                    isValid = true,
                    message = $"Acknowledged alarm for {tankNumber}",
                    tankNumber = tankNumber,
                    status = status
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AckOperationAlarm] ERROR: {ex.Message}");
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        // ========================================
        // ✅ NEW: Check and Trigger Level Alarms from Web
        // Ini akan check alarm H/L/HH/LL berdasarkan Tank Configuration
        // TANPA membutuhkan ATG Service
        // ========================================
        [HttpGet]
        public IActionResult CheckAndTriggerLevelAlarms()
        {
            try
            {
                var tanks = _context.Tank.Where(t => t.IsUsed == true).ToList();
                var liveDataList = _context.Tank_Live_Data.ToList();
                
                int alarmsTriggered = 0;
                int alarmsCleared = 0;
                var debugInfo = new List<string>();

                foreach (var tank in tanks)
                {
                    var liveData = liveDataList.FirstOrDefault(ld => ld.Tank_Number == tank.Tank_Name);
                    if (liveData == null) continue;

                    double currentLevel = liveData.Level ?? 0;
                    double currentTemp = liveData.Temperature ?? 0;
                    int? previousStatus = liveData.Alarm_Status;
                    bool ackFromDb = liveData.Ack ?? true;

                    debugInfo.Add($"[CHECK] Tank={tank.Tank_Name}, Level={currentLevel}mm, PrevStatus={GetAlarmTypeName(previousStatus)}");

                    // ✅ Definisi alarm levels dengan NULL SAFETY - SAMA dengan WorkerXmlMode
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

                    // ========================================================
                    // ✅ ALARM LOGIC (SAMA dengan WorkerXmlMode)
                    // ========================================================
                    if (currentAlarmCondition != null)
                    {
                        // CASE 1: ALARM BARU (TRANSISI DARI NORMAL → ALARM)
                        if (previousStatus == null || previousStatus == 0)
                        {
                            liveData.Ack = true;  // BERBUNYI!
                            debugInfo.Add($"  [NEW] {currentAlarmCondition} → Ack=TRUE ✅");
                            alarmsTriggered++;
                        }
                        // CASE 2: ALARM BERUBAH JENIS
                        // ========================================
                        else if (previousStatus != currentAlarmStatus)
                        {
                            liveData.Ack = true;  // BERBUNYI!
                            debugInfo.Add($"  [CHANGED] {GetAlarmTypeName(previousStatus)} → {currentAlarmCondition} → Ack=TRUE ✅");
                            alarmsTriggered++;
                        }
                        // CASE 3: ALARM SAMA
                        // ========================================
                        else
                        {
                            if (ackFromDb)
                            {
                                liveData.Ack = true;  // TETAP BERBUNYI
                                debugInfo.Add($"  [SAME] {currentAlarmCondition}, Not ACKed → Ack=TRUE");
                            }
                            else
                            {
                                // Sudah di-ACK, cek re-trigger untuk critical alarm
                                bool isCriticalAlarm = (currentAlarmStatus == 4 || currentAlarmStatus == 1); // HH atau LL
                                double lastLevel = liveData.Level ?? 0;
                                bool hasLevelChanged = Math.Abs(currentLevel - lastLevel) >= 1.0;
                                
                                if (isCriticalAlarm && hasLevelChanged)
                                {
                                    liveData.Ack = true;
                                    debugInfo.Add($"  [RE-TRIGGER] {currentAlarmCondition}, Level changed → Ack=TRUE ✅");
                                    alarmsTriggered++;
                                }
                                else
                                {
                                    liveData.Ack = false;
                                    debugInfo.Add($"  [SILENT] {currentAlarmCondition}, Already ACKed");
                                }
                            }
                        }

                        // Set alarm message
                        liveData.Alarm_Status = currentAlarmStatus;
                        liveData.AlarmMessage = $"Alarm status {currentAlarmCondition} On {tank.Tank_Name} !!!";
                    }
                    else
                    {
                        // CASE 4: ALARM HILANG (KEMBALI NORMAL)
                        if (previousStatus != null && previousStatus > 0)
                        {
                            debugInfo.Add($"  [CLEARED] {GetAlarmTypeName(previousStatus)} → NORMAL");
                            alarmsCleared++;
                        }
                        
                        liveData.Alarm_Status = null;
                        liveData.AlarmMessage = null;
                        liveData.Ack = true;
                    }
                }

                _context.SaveChanges();

                return Json(new
                {
                    isValid = true,
                    message = $"Checked {tanks.Count} tanks. Triggered: {alarmsTriggered}, Cleared: {alarmsCleared}",
                    alarmsTriggered = alarmsTriggered,
                    alarmsCleared = alarmsCleared,
                    debug = string.Join("\n", debugInfo)
                });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult TriggerAlarm()
        {
            try
            {
                CheckAndUpdateLevelAlarms();

                // ✅ Load Tank Configuration for validation
                var tankConfigs = _context.Tank.ToList();

                var tankLiveData = _context.Tank_Live_Data
                    .Where(t => t.Alarm_Status != null && t.Alarm_Status > 0 && t.Ack == true)
                    .ToList();

                List<Tank> tanks = _context.Tank.ToList();
                List<Alarm> alarms = new List<Alarm>();

                foreach (var liveData in tankLiveData)
                {
                    var tank = tanks.FirstOrDefault(t => t.Tank_Name == liveData.Tank_Number);
                    if (tank == null) continue;

                    // ✅ VALIDATE: Check if UseAlarm is enabled for this alarm type
                    if (!IsAlarmValidForTank(tank, liveData.Alarm_Status)) continue;

                    if (liveData.Alarm_Status.HasValue && liveData.Alarm_Status.Value > 0 && !string.IsNullOrEmpty(liveData.AlarmMessage))
                    {
                        alarms.Add(new Alarm
                        {
                            TankName = tank.Tank_Name,
                            Type = GetAlarmTypeName(liveData.Alarm_Status),
                            Message = liveData.AlarmMessage
                        });
                    }
                }

                return Json(new { isValid = alarms.Count > 0, data = alarms });
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message, data = new List<Alarm>() });
            }
        }

        private void CheckAndUpdateLevelAlarms()
        {
            var tanks = _context.Tank.Where(t => t.IsUsed == true).ToList();
            var liveDataList = _context.Tank_LiveDataTMS.ToList();

            foreach (var tank in tanks)
            {
                var liveData = liveDataList.FirstOrDefault(ld => ld.Tank_Number == tank.Tank_Name);
                if (liveData == null) continue;

                double currentLevel = liveData.Level ?? 0;
                double currentTemp = liveData.Temperature ?? 0;
                int? previousStatus = liveData.Alarm_Status;
                bool ackFromDb = liveData.Ack ?? true;

                string currentAlarmCondition = null;

                // ✅ Check alarm conditions - ONLY if UseAlarm is enabled
                if ((tank.UseAlarmHH ?? false) && currentLevel >= (tank.LevelHH ?? double.MaxValue))
                {
                    currentAlarmCondition = "HH";
                }
                else if ((tank.UseAlarmH ?? false) && currentLevel >= (tank.LevelH ?? double.MaxValue))
                {
                    bool belowHH = !(tank.UseAlarmHH ?? false) || currentLevel < (tank.LevelHH ?? double.MaxValue);
                    if (belowHH) currentAlarmCondition = "H";
                }
                else if ((tank.UseAlarmLL ?? false) && currentLevel <= (tank.LevelLL ?? 0))
                {
                    currentAlarmCondition = "LL";
                }
                else if ((tank.UseAlarmL ?? false) && currentLevel <= (tank.LevelL ?? 0))
                {
                    bool aboveLL = !(tank.UseAlarmLL ?? false) || currentLevel > (tank.LevelLL ?? 0);
                    if (aboveLL) currentAlarmCondition = "L";
                }
                else if ((tank.UseAlarmTempH ?? false) && currentTemp >= (tank.TempH ?? double.MaxValue))
                {
                    currentAlarmCondition = "TempH";
                }
                else if ((tank.UseAlarmTempL ?? false) && currentTemp <= (tank.TempL ?? 0))
                {
                    currentAlarmCondition = "TempL";
                }

                int? currentAlarmStatus = currentAlarmCondition != null ? ConvertAlarmStringToInt(currentAlarmCondition) : (int?)null;

                if (currentAlarmCondition != null)
                {
                    if (previousStatus == null || previousStatus == 0 || previousStatus != currentAlarmStatus)
                    {
                        liveData.Ack = true;
                    }
                    else if (ackFromDb)
                    {
                        liveData.Ack = true;
                    }
                    else
                    {
                        liveData.Ack = false;
                    }

                    liveData.Alarm_Status = currentAlarmStatus;
                    liveData.AlarmMessage = $"Alarm status {currentAlarmCondition} On {tank.Tank_Name} !!!";
                }
                else
                {
                    // ✅ Clear alarm status if no condition met OR alarm disabled
                    liveData.Alarm_Status = null;
                    liveData.AlarmMessage = null;
                    liveData.Ack = true;
                }
            }

            _context.SaveChanges();
        }

        private void CheckAndUpdateOperationAlarms()
        {
            double FLOWRATE_TOLERANCE_STAGNANT = _configuration.GetValue<double>("AlarmSettings:StagnantFlowrateToleranceKLH", 0.05);
            double FLOWRATE_TOLERANCE_STANDBY = _configuration.GetValue<double>("AlarmSettings:StandbyFlowrateToleranceKLH", 20.0);
            int STANDBY_LEVEL_CHANGE_TOLERANCE = _configuration.GetValue<int>("AlarmSettings:StandbyLevelChangeToleranceMM", 10);
            double FLOWRATE_TOLERANCE_SALES = _configuration.GetValue<double>("AlarmSettings:SalesFlowrateToleranceKLH", 20.0);
            int SALES_LEVEL_CHANGE_TOLERANCE = _configuration.GetValue<int>("AlarmSettings:SalesLevelChangeToleranceMM", 5);
            int LEVEL_FLUCTUATION_MM = _configuration.GetValue<int>("AlarmSettings:LevelFluctuationToleranceMM", 2);

            var movements = _context.Tank_Movement
                .Where(m => m.Status != null)
                .ToList();

            var liveData = _context.Tank_Live_Data.ToList();
            bool hasChanges = false;

            foreach (var tm in movements)
            {
                // ✅ NEW: SKIP jika DisableOperationAlarm = true (khusus T-10 dan T-11)
                if (tm.DisableOperationAlarm)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[OPERATION ALARM] Tank={tm.Tank_Number} - SKIPPED (DisableOperationAlarm=true)");
                    continue;
                }

                var tld = liveData.FirstOrDefault(ld => ld.Tank_Number == tm.Tank_Number);
                if (tld == null) continue;

                bool alarmConditionMet = false;
                double currentFlowrate = tld.Flowrate ?? 0;
                double absFlowrate = Math.Abs(currentFlowrate);
                int currentLevel = (int)(tld.Level ?? 0);
                int previousLevel = tm.Level ?? currentLevel;
                int levelDelta = currentLevel - previousLevel;
                int absLevelDelta = Math.Abs(levelDelta);

                bool isLevelStable = absLevelDelta <= LEVEL_FLUCTUATION_MM;

                // RECEIVING (Status = 1)
                if (tm.Status == 1)
                {
                    // ✅ Kondisi 1: Target tercapai
                    if (tm.TargetLevel > 0 && currentLevel >= tm.TargetLevel)
                    {
                        alarmConditionMet = true;
                    }
                    // ✅ Kondisi 2: Flowrate NEGATIF (flow keluar saat seharusnya masuk)
                    else if (currentFlowrate <= -FLOWRATE_TOLERANCE_STAGNANT)
                    {
                        alarmConditionMet = true;
                    }
                    // ✅ Kondisi 3: Level TURUN signifikan (anomaly)
                    else if (levelDelta <= -STANDBY_LEVEL_CHANGE_TOLERANCE)
                    {
                        alarmConditionMet = true;
                    }

                    if (tm.Level != currentLevel)
                    {
                        tm.Level = currentLevel;
                        hasChanges = true;
                    }
                }
                // SALES (Status = 2)
                else if (tm.Status == 2)
                {
                    bool targetReached = tm.TargetLevel > 0 && currentLevel <= tm.TargetLevel;
                    bool hasPositiveFlowrate = currentFlowrate >= FLOWRATE_TOLERANCE_SALES;
                    bool levelIncreasing = levelDelta >= SALES_LEVEL_CHANGE_TOLERANCE;
            
                    alarmConditionMet = targetReached || hasPositiveFlowrate || levelIncreasing;
            
                    if (tm.Level != currentLevel)
                    {
                        tm.Level = currentLevel;
                        hasChanges = true;
                    }
                }
                // STANDBY/IDLE (Status = 0)
                else if (tm.Status == 0)
                {
                    bool hasFlowrateAnomaly = absFlowrate >= FLOWRATE_TOLERANCE_STANDBY;
                    bool hasLevelChangeAnomaly = absLevelDelta >= STANDBY_LEVEL_CHANGE_TOLERANCE;
            
                    alarmConditionMet = hasFlowrateAnomaly || hasLevelChangeAnomaly;
            
                    if (!alarmConditionMet && tm.Level != currentLevel)
                    {
                        tm.Level = currentLevel;
                        hasChanges = true;
                    }
                }

                // ACK LOGIC
                if (alarmConditionMet)
                {
                    // Kondisi alarm TERPENUHI
                }
                else
                {
                    if (tm.OperationAlarmAck)
                    {
                        tm.OperationAlarmAck = false;
                        hasChanges = true;
                    }
            
                    if (tm.Status == 0 && tm.Level != currentLevel)
                    {
                        tm.Level = currentLevel;
                        hasChanges = true;
                    }
                }

                // Stagnant check for RECEIVING
                if (tm.Status == 1 && tm.StagnantThresholdMinutes > 0)
                {
                    bool isStagnant = false;

                    if (absFlowrate < FLOWRATE_TOLERANCE_STAGNANT && isLevelStable)
                    {
                        if (tm.LastFlowrateChangeTime.HasValue)
                        {
                            var duration = DateTime.Now - tm.LastFlowrateChangeTime.Value;
                            isStagnant = duration.TotalMinutes >= tm.StagnantThresholdMinutes;
                        }
                        else
                        {
                            tm.LastFlowrateChangeTime = DateTime.Now;
                            hasChanges = true;
                        }
                    }
                    else
                    {
                        tm.LastFlowrateChangeTime = DateTime.Now;
                        hasChanges = true;
                    }

                    if (isStagnant && !tm.StagnantAlarm)
                    {
                        tm.StagnantAlarm = true;
                        hasChanges = true;
                    }
                    else if (!isStagnant && tm.StagnantAlarm)
                    {
                        tm.StagnantAlarm = false;
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                _context.SaveChanges();
            }
        }
    }
}
