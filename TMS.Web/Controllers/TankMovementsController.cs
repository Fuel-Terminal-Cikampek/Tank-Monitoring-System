//using CSL.Web.Models;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using System.Diagnostics;
//using Microsoft.AspNetCore.Authorization;
//using System.Linq;
//using System.Linq.Dynamic.Core;
//using TMS.Web.Models;
//using System.Collections.Generic;
//using System;
//using TMS.Web.Areas.Identity.Data;
//using TMS.Models;
//using DocumentFormat.OpenXml.InkML;
//using DocumentFormat.OpenXml.Office2010.Excel;
//using System.Threading.Tasks;
//using CSL.Web;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;

//namespace TMS.Web.Controllers
//{
//    public class TankMovementsController : Controller
//    {
//        private readonly TMSContext _context;
//        private readonly IConfiguration _configuration;
//        public TankMovementsController(TMSContext context, IConfiguration configuration)
//        {
//            _context = context;
//            _configuration = configuration;
//        }
//        // GET: TankMovementsControllerController
//        public ActionResult Index()
//        {
//            return View();
//        }
//        public IActionResult LoadData()
//        {
//            try
//            {
//                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
//                var start = Request.Form["start"].FirstOrDefault();
//                var length = Request.Form["length"].FirstOrDefault();
//                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
//                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
//                var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();
//                int pageSize = length != null ? Convert.ToInt32(length) : 0;
//                int skip = start != null ? Convert.ToInt32(start) : 0;
//                int recordsTotal = 0;
                
//                CalculationTime();
                
//                // ✅ FIX: Use LEFT JOIN untuk Tank_Movement supaya semua tank muncul
//                var tank = (from t in _context.Tank
//                            join p in _context.Master_Product on t.Product_ID equals p.Product_Code
//                            join q in _context.Tank_Live_Data on t.Tank_Name equals q.Tank_Number
//                            join r in _context.Tank_Movement on t.Tank_Name equals r.Tank_Number into movementGroup
//                            from r in movementGroup.DefaultIfEmpty()  // LEFT JOIN
//                            select new
//                            {
//                                t.Tank_Name,
//                                p.Product_Name,
//                                Status = r.Status,
//                                TargetLevel = r != null ? (r.TargetLevel ?? t.Height_Safe_Capacity) : t.Height_Safe_Capacity,
//                                t.Height_Safe_Capacity,
//                                q.LiquidLevel,
//                                DelLevel = (r != null ? (r.TargetLevel ?? t.Height_Safe_Capacity) : t.Height_Safe_Capacity) - q.LiquidLevel,
//                                q.FlowRateKLperH,
//                                AlarmTimeStamp = r != null ? r.AlarmTimeStamp : (TimeSpan?)null,
//                                EstimationTimeStamp = r != null ? r.EstimationTimeStamp : (TimeSpan?)null,
//                                Alarm = r != null && r.Alarm
//                            });
        
//                // Sorting  
//                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
//                {
//                    tank = tank.OrderBy(sortColumn + " " + sortColumnDirection);
//                }

//                // Total number of rows counts   
//                recordsTotal = tank.Count();
//                // Paging   
//                var data = tank.Skip(skip).Take(pageSize).ToList();
//                // Returning Json Data  
//                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
//            }
//            catch (Exception)
//            {
//                throw;
//            }
//        }
//        private void CalculationTime()
//        {
//            var movement = _context.Tank_Movement.Where(item => item.Status == true).ToList();
//            if (movement.Count() > 0 && movement!=null)
//            {
//                foreach(var item in movement)
//                {
//                    var tank = _context.Tank.FirstOrDefault(i => i.Tank_Name == item.Tank_Number);
//                    var live = _context.Tank_Live_Data.FirstOrDefault(i => i.Tank_Number == item.Tank_Number);

//                    // ✅ FIX: Gunakan TargetLevel dari Tank_Movement (bukan Height_Safe_Capacity dari master)
//                    var targetLevel = item.TargetLevel ?? tank.Height_Safe_Capacity;

//                    // ✅ FIX: Convert Flowrate dari KL/h ke m/s
//                    // Flowrate dalam KL/h, perlu convert ke m/s untuk calculation
//                    var flowrateKLperH = live.Flowrate ?? 0;
//                    var flowrateMperS = ConvertFlowrateToMeterPerSecond(flowrateKLperH, tank.RaisePerMM);

//                    // ✅ STAGNANT ALARM DETECTION
//                    DetectStagnantCondition(item, flowrateKLperH);

//                    // ✅ Calculate estimation (NULL jika flowrate = 0)
//                    item.EstimationTimeStamp = flowrateMperS == 0
//                        ? null
//                        : GetEstimation(flowrateMperS, targetLevel, (double)(live.Level ?? 0));

//                    _context.Update(item);
//                    _context.SaveChanges();
//                }
//            }
//        }

//        // ✅ NEW: Detect stagnant condition (no flowrate during active monitoring)
//        private void DetectStagnantCondition(TankMovement item, double currentFlowrate)
//        {
//            // Read threshold from appsettings.json, fallback to DB value, then default to 5 minutes
//            int configThreshold = _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);
//            int thresholdMinutes = item.StagnantThresholdMinutes ?? configThreshold;
//            double flowrateThreshold = 0.01; // KL/h - consider as "no flow" if below this

//            // Check if flowrate is stagnant (near zero)
//            bool isStagnant = Math.Abs(currentFlowrate) < flowrateThreshold;

//            Console.WriteLine($"[DetectStagnant] Tank: {item.Tank_Number}, Flowrate: {currentFlowrate:F3}, IsStagnant: {isStagnant}, Threshold: {thresholdMinutes}min");

//            if (isStagnant)
//            {
//                // Initialize LastFlowrateChangeTime if null
//                if (item.LastFlowrateChangeTime == null)
//                {
//                    item.LastFlowrateChangeTime = DateTime.Now;
//                    item.StagnantAlarm = false;
//                    Console.WriteLine($"[DetectStagnant] Tank: {item.Tank_Number} - Initialized LastFlowrateChangeTime");
//                }
//                else
//                {
//                    // ✅ FIX: Calculate duration from MAX(AlarmTimeStamp, LastFlowrateChangeTime)
//                    // Setelah ACK, AlarmTimeStamp akan lebih baru → gunakan sebagai baseline
//                    DateTime baselineTime = item.LastFlowrateChangeTime.Value;

//                    if (item.AlarmTimeStamp.HasValue)
//                    {
//                        DateTime alarmDateTime = DateTime.Today.Add(item.AlarmTimeStamp.Value);
//                        if (alarmDateTime > DateTime.Now)
//                        {
//                            alarmDateTime = alarmDateTime.AddDays(-1);
//                        }
//                        if (alarmDateTime > baselineTime)
//                        {
//                            baselineTime = alarmDateTime;
//                            Console.WriteLine($"[DetectStagnant] Using AlarmTimeStamp as baseline: {alarmDateTime}");
//                        }
//                    }

//                    var stagnantDuration = DateTime.Now - baselineTime;
//                    Console.WriteLine($"[DetectStagnant] Tank: {item.Tank_Number} - Stagnant for {stagnantDuration.TotalMinutes:F1} min (threshold: {thresholdMinutes} min), Baseline: {baselineTime}");

//                    if (stagnantDuration.TotalMinutes >= thresholdMinutes)
//                    {
//                        item.StagnantAlarm = true;
//                        Console.WriteLine($"[DetectStagnant] Tank: {item.Tank_Number} - ⚠️ STAGNANT ALARM TRIGGERED!");
//                    }
//                }
//            }
//            else
//            {
//                // Flowrate is active, reset stagnant tracking
//                item.LastFlowrateChangeTime = DateTime.Now;
//                item.StagnantAlarm = false;
//                Console.WriteLine($"[DetectStagnant] Tank: {item.Tank_Number} - Flowrate active, reset stagnant tracking");
//            }
//        }

//        // ✅ NEW: Convert Flowrate dari KL/h ke meter/second
//        private double ConvertFlowrateToMeterPerSecond(double flowrateKLperH, double raisePerMM)
//        {
//            if (flowrateKLperH == 0 || raisePerMM == 0) return 0;

//            // Flowrate KL/h → Liter/h → Liter/s → mm/s → m/s
//            var literPerHour = flowrateKLperH * 1000;  // KL to L
//            var literPerSecond = literPerHour / 3600;   // per hour to per second
//            var mmPerSecond = literPerSecond / raisePerMM;  // Liter to mm
//            var meterPerSecond = mmPerSecond / 1000;    // mm to meter

//            return meterPerSecond;
//        }
//        private TimeSpan GetEstimation(double flowrate, double height, double preset)
//        {
//            var delLevel = height - preset;
//            var secondsToAdd =  delLevel/ flowrate;
//            var seconds = Math.Round(secondsToAdd, 0);
//            // Create a new TimeSpan with the desired number of seconds
//            TimeSpan timeSpanToAdd = TimeSpan.FromSeconds(seconds);
//            return timeSpanToAdd;
//        }
//        // GET: Tanks/Config
//        [NoDirectAccess]
//        public async Task<IActionResult> Config(string id = null)
//        {
//            if (string.IsNullOrEmpty(id))
//            {
//                return View(new Tank());
//            }

//            else
//            {
//                var tank = await _context.Tank.FirstOrDefaultAsync(t => t.Tank_Name == id);
//                if (tank == null)
//                {
//                    return NotFound();
//                }
//                // Load TankLiveData and TankMovement separately - not via navigation property
//                var liveData = _context.Tank_Live_Data.FirstOrDefault(i => i.Tank_Number == tank.Tank_Name);
//                var movement = _context.Tank_Movement.FirstOrDefault(i => i.Tank_Number == tank.Tank_Name);

//                return View(tank);
//            }
//        }
//        public new IActionResult ViewData(string id)
//        {
//            // id is now Tank_Name (string), not int TankId
//            var tank = _context.Tank.FirstOrDefault(i=>i.Tank_Name == id);
//            if (tank == null)
//            {
//                return Json(new {isValid = false});
//            }
//            else
//            {
//                // Load TankLiveData and TankMovement separately - not via navigation property
//                var liveData = _context.Tank_Live_Data.FirstOrDefault(i => i.Tank_Number == tank.Tank_Name);
//                var movement = _context.Tank_Movement.FirstOrDefault(i => i.Tank_Number == tank.Tank_Name);

//                return Json(new {isValid=true,data=tank});
//            }
//        }
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Config(string id, [Bind("TankId,HeightSafeCapacity,VolumeSafeCapacity, tankLiveData, tankMovement")] Tank tank)
//        {
//            try
//            {
//                var checktank = _context.Tank.FirstOrDefault(item=>item.Tank_Name == id);
//                if(tank != null)
//                {                  
//                   if(tank.tankMovement!=null)
//                    {
//                        var movement = _context.Tank_Movement.FirstOrDefault(item=>item.Tank_Number == id);
//                        if (movement != null)
//                        {
//                            movement.Status = (bool)tank.tankMovement.Status;
//                            TimeSpan time =(TimeSpan)tank.tankMovement.AlarmTimeStamp;
//                            movement.IsLevel = tank.tankMovement.IsLevel;
//                            if((bool) movement.IsLevel)
//                            {
//                                checktank.Height_Safe_Capacity = tank.Height_Safe_Capacity;
//                            }
//                            else
//                            {
//                                checktank.VolumeSafeCapacity = tank.VolumeSafeCapacity;
//                            }
//                            movement.AlarmTimeStamp = time;
//                            _context.Update(movement);
//                        }
//                    }
//                    _context.Update(checktank);
//                    await _context.SaveChangesAsync();
//                    return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Tank.ToList()) });


//                }
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                throw;
//            }
//            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "Config", tank) });
//        }
//        public IActionResult GetDataByTankId(string tankName)
//        {
//            try
//            {
//                var tank = (from t in _context.Tank
//                            join l in _context.Tank_Live_Data on t.Tank_Name equals l.Tank_Number
//                            join s in _context.Tank_Movement on t.Tank_Name equals s.Tank_Number
//                            select new
//                            {
//                                TankName = t.Tank_Name,
//                                t.Height_Safe_Capacity,
//                                l.FlowRateKLperH,
//                                s.EstimationTimeStamp

//                            }).FirstOrDefault(t => t.TankName == tankName);
//                if(tank != null)
//                {
//                    return Json(new { isValid = true, data = tank });
//                }

//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                throw;
//            }
//            return Json(new { isValid = false, data = "Tidak diterimukan data"});
//        }

//        // ✅ FIXED: Endpoint untuk check Tank Movement alarm (approaching target & stagnant)
//        // Ini dipanggil oleh tesAlarm() di _LayoutNew.cshtml setiap 3 detik
//        [HttpGet]
//        public IActionResult TriggerTankMovementAlarm()
//        {
//            try
//            {
//                Console.WriteLine("[TriggerTankMovementAlarm] Checking alarms...");

//                // ✅ FIX: Auto-close old Tank_Movement records (older than 7 days)
//                var cutoffDate = DateTime.Now.AddDays(-7);
//                var oldMovements = _context.Tank_Movement
//                    .Where(m => m.Status == 0 && m.TimeStamp < cutoffDate)
//                    .ToList();

//                if (oldMovements.Count > 0)
//                {
//                    Console.WriteLine($"[TriggerTankMovementAlarm] Auto-closing {oldMovements.Count} old movements (> 7 days)");
//                    foreach (var old in oldMovements)
//                    {
//                        old.Status = 0;
//                        old.StagnantAlarm = false;
//                    }
//                    _context.SaveChanges();
//                }

//                // ✅ IMPORTANT: Recalculate stagnant condition ONLY for RECENT active movements (< 7 days)
//                var activeMovements = _context.Tank_Movement
//                    .Where(m => m.Status == 0 && m.TimeStamp >= cutoffDate)
//                    .ToList();

//                Console.WriteLine($"[TriggerTankMovementAlarm] Found {activeMovements.Count} active recent movements (< 7 days)");

//                var tanks = _context.Tank.ToList();
//                var liveDataList = _context.Tank_Live_Data.ToList();
//                var alarms = new List<object>();

//                foreach (var movement in activeMovements)
//                {
//                    var tank = tanks.FirstOrDefault(t => t.Tank_Name == movement.Tank_Number);
//                    var liveData = liveDataList.FirstOrDefault(l => l.Tank_Number == movement.Tank_Number);

//                    if (tank == null || liveData == null) continue;

//                    // ✅ FIX: Auto-complete if target level reached (with 100mm tolerance)
//                    var currentLevel = liveData.Level ?? 0;
//                    var targetLevel = movement.TargetLevel ?? 0;
//                    if (targetLevel > 0 && currentLevel >= (targetLevel - 100))
//                    {
//                        Console.WriteLine($"[TriggerTankMovementAlarm] ✅ Tank {movement.Tank_Number} reached target! Current: {currentLevel}, Target: {targetLevel}. Auto-completing...");
//                        movement.Status = 0; // Stop monitoring
//                        movement.StagnantAlarm = false;
//                        _context.Update(movement);
//                        continue; // Skip further checks for this tank
//                    }

//                    // ✅ Re-calculate stagnant condition
//                    var currentFlowrate = Math.Abs(liveData.Flowrate ?? 0);
//                    int thresholdMinutes = movement.StagnantThresholdMinutes ??
//                        _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);
//                    double flowrateThreshold = 0.01;
//                    bool isStagnant = currentFlowrate < flowrateThreshold;

//                    Console.WriteLine($"[TriggerTankMovementAlarm] Tank: {movement.Tank_Number}, Flowrate: {currentFlowrate:F3} KL/h, IsStagnant: {isStagnant}");

//                    if (isStagnant)
//                    {
//                        if (movement.LastFlowrateChangeTime == null)
//                        {
//                            movement.LastFlowrateChangeTime = DateTime.Now;
//                            movement.StagnantAlarm = false;
//                        }
//                        else
//                        {
//                            // ✅ FIX: Calculate duration from MAX(AlarmTimeStamp, LastFlowrateChangeTime)
//                            // Setelah ACK, AlarmTimeStamp akan lebih baru → gunakan sebagai baseline
//                            DateTime baselineTime = movement.LastFlowrateChangeTime.Value;

//                            // Check if AlarmTimeStamp is more recent (e.g., after ACK)
//                            if (movement.AlarmTimeStamp.HasValue)
//                            {
//                                DateTime alarmDateTime = DateTime.Today.Add(movement.AlarmTimeStamp.Value);
//                                // Handle midnight rollover
//                                if (alarmDateTime > DateTime.Now)
//                                {
//                                    alarmDateTime = alarmDateTime.AddDays(-1);
//                                }

//                                // Use the more recent timestamp as baseline
//                                if (alarmDateTime > baselineTime)
//                                {
//                                    baselineTime = alarmDateTime;
//                                    Console.WriteLine($"[TriggerTankMovementAlarm] Using AlarmTimeStamp as baseline: {alarmDateTime}");
//                                }
//                            }

//                            var stagnantDuration = DateTime.Now - baselineTime;
//                            Console.WriteLine($"[TriggerTankMovementAlarm] Tank: {movement.Tank_Number}, StagnantDuration: {stagnantDuration.TotalMinutes:F1} min, Threshold: {thresholdMinutes} min, Baseline: {baselineTime}");

//                            if (stagnantDuration.TotalMinutes >= thresholdMinutes)
//                            {
//                                // ✅ FIX: Set AlarmTimeStamp saat alarm pertama kali trigger (transition false → true)
//                                if (movement.StagnantAlarm == false)
//                                {
//                                    movement.AlarmTimeStamp = TimeSpan.FromHours(DateTime.Now.Hour)
//                                        .Add(TimeSpan.FromMinutes(DateTime.Now.Minute))
//                                        .Add(TimeSpan.FromSeconds(DateTime.Now.Second));
//                                    Console.WriteLine($"[TriggerTankMovementAlarm] 🔔 ALARM FIRST TRIGGERED at {movement.AlarmTimeStamp} for {movement.Tank_Number}!");
//                                }
//                                movement.StagnantAlarm = true;
//                                Console.WriteLine($"[TriggerTankMovementAlarm] ⚠️ ALARM ACTIVE for {movement.Tank_Number}!");
//                            }
//                        }
//                    }
//                    else
//                    {
//                        movement.LastFlowrateChangeTime = DateTime.Now;
//                        movement.StagnantAlarm = false;
//                        // ✅ FIX: JANGAN reset AlarmTimeStamp saat flowrate aktif
//                        // AlarmTimeStamp hanya di-update saat ACK atau alarm pertama trigger
//                        // Duration akan dihitung dari MAX(AlarmTimeStamp, LastFlowrateChangeTime)
//                    }

//                    _context.Update(movement);

//                    // ✅ Add to alarm list if StagnantAlarm is true
//                    if (movement.StagnantAlarm)
//                    {
//                        // ✅ FIX: Duration dihitung dari waktu yang TERDEKAT (MAX)
//                        DateTime? alarmDateTime = null;
//                        if (movement.AlarmTimeStamp.HasValue)
//                        {
//                            alarmDateTime = DateTime.Today.Add(movement.AlarmTimeStamp.Value);
//                            if (alarmDateTime > DateTime.Now)
//                            {
//                                alarmDateTime = alarmDateTime.Value.AddDays(-1);
//                            }
//                        }

//                        DateTime? latestTimestamp = null;
//                        if (alarmDateTime.HasValue && movement.LastFlowrateChangeTime.HasValue)
//                        {
//                            latestTimestamp = alarmDateTime > movement.LastFlowrateChangeTime
//                                ? alarmDateTime
//                                : movement.LastFlowrateChangeTime;
//                        }
//                        else if (alarmDateTime.HasValue)
//                        {
//                            latestTimestamp = alarmDateTime;
//                        }
//                        else if (movement.LastFlowrateChangeTime.HasValue)
//                        {
//                            latestTimestamp = movement.LastFlowrateChangeTime;
//                        }

//                        var stagnantDuration = latestTimestamp.HasValue
//                            ? (DateTime.Now - latestTimestamp.Value).TotalMinutes
//                            : 0;

//                        alarms.Add(new
//                        {
//                            tankName = tank.Tank_Name,
//                            type = "No Flowrate",
//                            message = $"Tank {tank.Tank_Name} tidak ada flowrate selama {Math.Round(stagnantDuration, 0)} menit",
//                            alarmType = "stagnant",
//                            stagnantMinutes = Math.Round(stagnantDuration, 0),
//                            thresholdMinutes = thresholdMinutes,
//                            currentFlowrate = currentFlowrate
//                        });
//                    }

//                    // Approaching target alarm (existing logic)
//                    if (movement.Alarm && movement.EstimationTimeStamp.HasValue)
//                    {
//                        alarms.Add(new
//                        {
//                            tankName = tank.Tank_Name,
//                            type = "Approaching Target",
//                            message = $"Tank {tank.Tank_Name} mendekati target level dalam {movement.EstimatedTimeString}",
//                            alarmType = "approaching"
//                        });
//                    }
//                }

//                // ✅ Save changes to database
//                _context.SaveChanges();

//                Console.WriteLine($"[TriggerTankMovementAlarm] Total alarms: {alarms.Count}");

//                return Json(new { isValid = alarms.Count > 0, data = alarms });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[TriggerTankMovementAlarm] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message, data = new List<object>() });
//            }
//        }

//        // ========================================
//        // ✅ ACK Tank Movement Alarm (No Flowrate / Stagnant)
//        // ========================================
//        [HttpPost]
//        public IActionResult AckTankMovementAlarm()
//        {
//            try
//            {
//                Console.WriteLine("[AckTankMovementAlarm] Acknowledging all stagnant alarms...");

//                // ✅ Reset StagnantAlarm untuk semua active movements
//                var activeMovements = _context.Tank_Movement
//                    .Where(m => m.Status == true && m.StagnantAlarm == true)
//                    .ToList();

//                Console.WriteLine($"[AckTankMovementAlarm] Found {activeMovements.Count} active stagnant alarms");

//                foreach (var movement in activeMovements)
//                {
//                    movement.StagnantAlarm = false;
//                    // ✅ FIX: ACK → Update AlarmTimeStamp ke waktu sekarang (bukan LastFlowrateChangeTime)
//                    // Duration akan dihitung dari AlarmTimeStamp karena ini waktu terakhir user melakukan aksi
//                    movement.AlarmTimeStamp = TimeSpan.FromHours(DateTime.Now.Hour)
//                        .Add(TimeSpan.FromMinutes(DateTime.Now.Minute))
//                        .Add(TimeSpan.FromSeconds(DateTime.Now.Second));
//                    Console.WriteLine($"[AckTankMovementAlarm] ACK: {movement.Tank_Number}, AlarmTimeStamp updated to {movement.AlarmTimeStamp}");
//                }

//                _context.SaveChanges();

//                return Json(new
//                {
//                    isValid = true,
//                    message = "Tank Movement alarms acknowledged",
//                    acknowledgedCount = activeMovements.Count
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[AckTankMovementAlarm] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ========================================
//        // ✅ GET Active Monitoring with Details (for monitoring modal)
//        // ========================================
//        [HttpGet]
//        public IActionResult GetActiveAlarmsWithDetails()
//        {
//            try
//            {
//                Console.WriteLine("[GetActiveAlarmsWithDetails] Fetching active monitoring tanks...");

//                // ✅ UPDATED: Query semua tank yang sedang dimonitor (Status = true)
//                // Tidak hanya yang alarm, tapi semua yang active monitoring
//                var activeMovements = _context.Tank_Movement
//                    .Where(m => m.Status == 0)
//                    .ToList();

//                Console.WriteLine($"[GetActiveAlarmsWithDetails] Found {activeMovements.Count} active monitoring tanks");

//                // Count alarms vs normal
//                var alarmCount = activeMovements.Count(m => m.StagnantAlarm == true);
//                var normalCount = activeMovements.Count - alarmCount;
//                Console.WriteLine($"[GetActiveAlarmsWithDetails] Breakdown: {alarmCount} alarms, {normalCount} normal");

//                // Join with Tank_LiveData to get current flowrate
//                var monitoringWithDetails = activeMovements.Select(movement =>
//                {
//                    var liveData = _context.Tank_Live_Data
//                        .FirstOrDefault(ld => ld.Tank_Number == movement.Tank_Number);

//                    // ✅ FIX: Duration dihitung dari waktu yang TERDEKAT (MAX)
//                    // - AlarmTimeStamp: waktu ACK atau alarm pertama trigger
//                    // - LastFlowrateChangeTime: waktu flowrate terakhir aktif
//                    DateTime? alarmDateTime = null;
//                    if (movement.AlarmTimeStamp.HasValue)
//                    {
//                        // Convert TimeSpan to DateTime (today's date + time)
//                        alarmDateTime = DateTime.Today.Add(movement.AlarmTimeStamp.Value);
//                        // Handle case where alarm was yesterday (timestamp is earlier than now but should be today)
//                        if (alarmDateTime > DateTime.Now)
//                        {
//                            alarmDateTime = alarmDateTime.Value.AddDays(-1);
//                        }
//                    }

//                    // Get the most recent timestamp (MAX)
//                    DateTime? latestTimestamp = null;
//                    if (alarmDateTime.HasValue && movement.LastFlowrateChangeTime.HasValue)
//                    {
//                        latestTimestamp = alarmDateTime > movement.LastFlowrateChangeTime
//                            ? alarmDateTime
//                            : movement.LastFlowrateChangeTime;
//                    }
//                    else if (alarmDateTime.HasValue)
//                    {
//                        latestTimestamp = alarmDateTime;
//                    }
//                    else if (movement.LastFlowrateChangeTime.HasValue)
//                    {
//                        latestTimestamp = movement.LastFlowrateChangeTime;
//                    }

//                    var duration = latestTimestamp.HasValue
//                        ? DateTime.Now - latestTimestamp.Value
//                        : TimeSpan.Zero;

//                    return new
//                    {
//                        tankNumber = movement.Tank_Number,
//                        lastFlowrateChangeTime = movement.LastFlowrateChangeTime,
//                        alarmTimeStamp = movement.AlarmTimeStamp,
//                        latestTimestamp = latestTimestamp,
//                        stagnantThresholdMinutes = movement.StagnantThresholdMinutes ?? 5,
//                        // ✅ FIX: Current Level harus dari Tank_LiveData (real-time), bukan Tank_Movement (snapshot)
//                        currentLevel = liveData?.Level ?? movement.Level,
//                        targetLevel = movement.TargetLevel,

//                        // ✅ Status alarm untuk color coding (merah/hijau)
//                        stagnantAlarm = movement.StagnantAlarm,
//                        status = movement.Status,

//                        // ✅ Flowrate dari Tank_LiveData
//                        flowrate = liveData?.Flowrate ?? 0.0,

//                        // ✅ Duration dalam format untuk display
//                        durationSeconds = (int)duration.TotalSeconds,
//                        durationDisplay = duration.Days > 0
//                            ? $"{duration.Days}d {duration.Hours}h {duration.Minutes}m"
//                            : duration.Hours > 0
//                                ? $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s"
//                                : $"{duration.Minutes}m {duration.Seconds}s",

//                        // Additional info
//                        temperature = liveData?.Temperature,
//                        density = liveData?.Density,
//                        volume = liveData?.Volume,
//                        tankMovementId = movement.Tank_Movement_ID,
//                        tankTicketId = movement.Tank_Ticket_ID
//                    };
//                }).ToList();

//                return Json(new
//                {
//                    isValid = true,
//                    count = monitoringWithDetails.Count,
//                    alarmCount = alarmCount,
//                    normalCount = normalCount,
//                    data = monitoringWithDetails
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[GetActiveAlarmsWithDetails] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message, data = new List<object>() });
//            }
//        }

//        // ========================================
//        // ✅ ACK Individual Tank Movement Alarm
//        // ========================================
//        [HttpPost]
//        public IActionResult AckIndividualAlarm(string tankNumber)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(tankNumber))
//                {
//                    return Json(new { isValid = false, message = "Tank number required" });
//                }

//                Console.WriteLine($"[AckIndividualAlarm] Acknowledging alarm for {tankNumber}...");

//                var movement = _context.Tank_Movement
//                    .FirstOrDefault(m => m.Tank_Number == tankNumber && m.Status == true && m.StagnantAlarm == true);

//                if (movement == null)
//                {
//                    return Json(new { isValid = false, message = $"No active alarm found for {tankNumber}" });
//                }

//                movement.StagnantAlarm = false;
//                // ✅ FIX: ACK → Update AlarmTimeStamp ke waktu sekarang (bukan LastFlowrateChangeTime)
//                // Duration akan dihitung dari AlarmTimeStamp karena ini waktu terakhir user melakukan aksi
//                movement.AlarmTimeStamp = TimeSpan.FromHours(DateTime.Now.Hour)
//                    .Add(TimeSpan.FromMinutes(DateTime.Now.Minute))
//                    .Add(TimeSpan.FromSeconds(DateTime.Now.Second));

//                _context.SaveChanges();

//                Console.WriteLine($"[AckIndividualAlarm] ACK success: {tankNumber}, AlarmTimeStamp updated to {movement.AlarmTimeStamp}");

//                return Json(new
//                {
//                    isValid = true,
//                    message = $"Alarm acknowledged for {tankNumber}",
//                    tankNumber = tankNumber
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[AckIndividualAlarm] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ========================================
//        // ✅ GET Alarm History (Stopped/Inactive) with Pagination
//        // ========================================
//        [HttpGet]
//        public IActionResult GetAlarmHistory(int page = 1, int pageSize = 10)
//        {
//            try
//            {
//                Console.WriteLine($"[GetAlarmHistory] Fetching history page {page}, pageSize {pageSize}");

//                // Query inactive Tank_Movement records (Status = false)
//                var query = _context.Tank_Movement
//                    .Where(m => m.Status == 0)
//                    .OrderByDescending(m => m.TimeStamp); // Latest first

//                // Total count
//                var totalCount = query.Count();

//                // Pagination
//                var history = query
//                    .Skip((page - 1) * pageSize)
//                    .Take(pageSize)
//                    .ToList();

//                Console.WriteLine($"[GetAlarmHistory] Found {history.Count} records (total: {totalCount})");

//                // Map to response
//                var historyData = history.Select(h => new
//                {
//                    id = h.Tank_Movement_ID,
//                    tankNumber = h.Tank_Number,
//                    timestamp = h.TimeStamp,
//                    targetLevel = h.TargetLevel,
//                    currentLevel = h.Level,
//                    stagnantThresholdMinutes = h.StagnantThresholdMinutes ?? 5,
//                    lastFlowrateChangeTime = h.LastFlowrateChangeTime,
//                    isManuallyConfigured = h.IsManuallyConfigured
//                }).ToList();

//                var hasMore = (page * pageSize) < totalCount;

//                return Json(new
//                {
//                    isValid = true,
//                    data = historyData,
//                    page = page,
//                    pageSize = pageSize,
//                    totalCount = totalCount,
//                    hasMore = hasMore
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[GetAlarmHistory] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message, data = new List<object>() });
//            }
//        }

//        // ========================================
//        // ✅ UPDATE Alarm History Record
//        // ========================================
//        [HttpPost]
//        public IActionResult UpdateHistory(int id, int? targetLevel, int? stagnantThresholdMinutes)
//        {
//            try
//            {
//                Console.WriteLine($"[UpdateHistory] Updating history ID={id}");

//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Movement_ID == id);

//                if (movement == null)
//                {
//                    return Json(new { isValid = false, message = $"Record ID={id} not found" });
//                }

//                // Update fields
//                if (targetLevel.HasValue)
//                {
//                    movement.TargetLevel = targetLevel.Value;
//                }

//                if (stagnantThresholdMinutes.HasValue)
//                {
//                    movement.StagnantThresholdMinutes = stagnantThresholdMinutes.Value;
//                }

//                _context.SaveChanges();

//                Console.WriteLine($"[UpdateHistory] Successfully updated ID={id}");

//                return Json(new
//                {
//                    isValid = true,
//                    message = "History record updated successfully"
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[UpdateHistory] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ========================================
//        // ✅ DELETE Alarm History Record
//        // ========================================
//        [HttpPost]
//        public IActionResult DeleteHistory(int id)
//        {
//            try
//            {
//                Console.WriteLine($"[DeleteHistory] Deleting history ID={id}");

//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Movement_ID == id);

//                if (movement == null)
//                {
//                    return Json(new { isValid = false, message = $"Record ID={id} not found" });
//                }

//                _context.Tank_Movement.Remove(movement);
//                _context.SaveChanges();

//                Console.WriteLine($"[DeleteHistory] Successfully deleted ID={id}");

//                return Json(new
//                {
//                    isValid = true,
//                    message = "History record deleted successfully"
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[DeleteHistory] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ========================================
//        // ✅ BULK DELETE Alarm History Records
//        // ========================================
//        [HttpPost]
//        public IActionResult BulkDeleteHistory([FromBody] List<int> ids)
//        {
//            try
//            {
//                Console.WriteLine($"[BulkDeleteHistory] Deleting {ids.Count} records");

//                var movements = _context.Tank_Movement
//                    .Where(m => ids.Contains(m.Tank_Movement_ID))
//                    .ToList();

//                if (movements.Count == 0)
//                {
//                    return Json(new { isValid = false, message = "No records found to delete" });
//                }

//                _context.Tank_Movement.RemoveRange(movements);
//                _context.SaveChanges();

//                Console.WriteLine($"[BulkDeleteHistory] Successfully deleted {movements.Count} records");

//                return Json(new
//                {
//                    isValid = true,
//                    message = $"{movements.Count} history records deleted successfully",
//                    deletedCount = movements.Count
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BulkDeleteHistory] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ✅ NEW: Aktivasi Tank Movement untuk receiving (ROT) operation
//        [HttpPost]
//        public IActionResult ActivateMonitoring(string tankNumber, int? targetLevel = null)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(tankNumber))
//                {
//                    return Json(new { isValid = false, message = "Tank number required" });
//                }

//                // ✅ FIX: Query hanya cari yang ACTIVE (Status = true)
//                // Jika tidak ada active monitoring → buat BARU
//                // Jika ada active monitoring → update (case: user update settings saat monitoring)
//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == tankNumber && m.Status == true);
//                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankNumber);

//                if (tank == null)
//                {
//                    return Json(new { isValid = false, message = $"Tank {tankNumber} not found" });
//                }

//                if (movement == null)
//                {
//                    // ✅ Create NEW Tank_Movement record (tidak update yang lama)
//                    Console.WriteLine($"[ActivateMonitoring] Creating NEW Tank_Movement for {tankNumber}");

//                    // ✅ FIX: Try to find latest record untuk ambil Tank_Ticket_ID
//                    var previousMovement = _context.Tank_Movement
//                        .Where(m => m.Tank_Number == tankNumber)
//                        .OrderByDescending(m => m.Tank_Movement_ID)
//                        .FirstOrDefault();

                    
//                    Console.WriteLine($"[ActivateMonitoring] Previous movement: ID={previousMovement?.Tank_Movement_ID}, Ticket={ticketId}");

//                    movement = new TankMovement
//                    {
//                        Tank_Number = tankNumber,
//                        Status = 0,
//                        TargetLevel = targetLevel ?? (int)tank.Height_Safe_Capacity,
//                        TimeStamp = DateTime.Now,
//                        LastFlowrateChangeTime = DateTime.Now,
//                        StagnantAlarm = false
//                    };
//                    _context.Tank_Movement.Add(movement);
//                }
//                else
//                {
//                    // ✅ Update existing ACTIVE record (case: update settings saat monitoring)
//                    Console.WriteLine($"[ActivateMonitoring] Updating existing ACTIVE Tank_Movement for {tankNumber}");
//                    movement.TargetLevel = targetLevel ?? movement.TargetLevel ?? (int)tank.Height_Safe_Capacity;
//                    movement.TimeStamp = DateTime.Now;
//                    movement.LastFlowrateChangeTime = DateTime.Now;
//                    movement.StagnantAlarm = false;
//                    _context.Update(movement);
//                }

//                _context.SaveChanges();

//                return Json(new {
//                    isValid = true,
//                    message = $"Tank {tankNumber} monitoring activated",
//                    tankNumber = tankNumber,
//                    targetLevel = movement.TargetLevel
//                });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ✅ NEW: Deaktivasi Tank Movement saat operasi selesai (CLOSE)
//        [HttpPost]
//        public IActionResult DeactivateMonitoring(string tankNumber)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(tankNumber))
//                {
//                    return Json(new { isValid = false, message = "Tank number required" });
//                }

//                // ✅ FIX: Query hanya cari yang ACTIVE (Status = true) untuk di-deactivate
//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == tankNumber && m.Status == 0);

//                if (movement != null)
//                {
//                    Console.WriteLine($"[DeactivateMonitoring] Deactivating Tank_Movement ID={movement.Tank_Movement_ID} for {tankNumber}");
//                    movement.Status = 0;
//                    movement.StagnantAlarm = false;
//                    movement.EstimationTimeStamp = null;
//                    _context.Update(movement);
//                    _context.SaveChanges();
//                }
//                else
//                {
//                    Console.WriteLine($"[DeactivateMonitoring] No active monitoring found for {tankNumber}");
//                }

//                return Json(new {
//                    isValid = true,
//                    message = $"Tank {tankNumber} monitoring deactivated"
//                });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        /// <summary>
//        /// Get Tank info for threshold modal
//        /// </summary>
//        [HttpGet]
//        public IActionResult GetTankInfo(string tankNumber)
//        {
//            try
//            {
//                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankNumber);
//                if (tank == null)
//                {
//                    return Json(new { isValid = false, message = "Tank not found" });
//                }

//                return Json(new
//                {
//                    isValid = true,
//                    tankName = tank.Tank_Name,
//                    heightSafeCapacity = tank.Height_Safe_Capacity,
//                    tankHeight = tank.Tank_Height
//                });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        /// <summary>
//        /// ✅ Get default values for Tank Movement threshold modal
//        /// </summary>
//        [HttpGet]
//        public IActionResult GetTankMovementDefaults(string tankNumber)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(tankNumber))
//                {
//                    return Json(new { isValid = false, message = "Tank number required" });
//                }

//                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankNumber);
//                if (tank == null)
//                {
//                    return Json(new { isValid = false, message = $"Tank {tankNumber} not found" });
//                }

//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == tankNumber);
//                int configStagnant = _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);

//                return Json(new
//                {
//                    isValid = true,
//                    tankNumber = tankNumber,
//                    targetLevel = movement?.TargetLevel ?? (int?)tank.Height_Safe_Capacity ?? 5000,
//                    alarmMinutes = movement?.AlarmTimeStamp?.TotalMinutes ?? 5,
//                    stagnantMinutes = movement?.StagnantThresholdMinutes ?? configStagnant
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[GetTankMovementDefaults] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        /// <summary>
//        /// ✅ Set Tank Movement threshold
//        /// </summary>
//        [HttpPost]
//        public IActionResult SetTankMovementThreshold([FromBody] TankMovementThresholdRequest request)
//        {
//            try
//            {
//                Console.WriteLine($"[SetTankMovementThreshold] Tank: {request.TankNumber}, Target: {request.TargetLevel}, Alarm: {request.AlarmMinutes}min, Stagnant: {request.StagnantThresholdMinutes}min, TicketId: {request.TankTicketId}");

//                if (string.IsNullOrEmpty(request.TankNumber))
//                {
//                    return Json(new { isValid = false, message = "Tank number required" });
//                }

//                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == request.TankNumber);
//                if (tank == null)
//                {
//                    return Json(new { isValid = false, message = $"Tank {request.TankNumber} not found" });
//                }

//                var targetLevel = request.TargetLevel > 0 ? request.TargetLevel : (int?)tank.Height_Safe_Capacity ?? 5000;
//                var alarmMinutes = request.AlarmMinutes > 0 ? request.AlarmMinutes : 5;
//                var stagnantMinutes = request.StagnantThresholdMinutes > 0 ? request.StagnantThresholdMinutes : 5;
//                var alarmTimeSpan = new TimeSpan(0, alarmMinutes, 0);

//                var liveData = _context.Tank_Live_Data.FirstOrDefault(l => l.Tank_Number == request.TankNumber);

//                // ✅ FIX: Only look for ACTIVE movement, not stopped ones
//                var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == request.TankNumber && m.Status == true);

//                if (movement == null)
//                {
//                    // ✅ FIX: Use TankTicketId from request if provided (from ROT Open)
//                    long? ticketId = request.TankTicketId;

//                    if (ticketId == null)
//                    {
//                        var previousMovement = _context.Tank_Movement
//                            .Where(m => m.Tank_Number == request.TankNumber)
//                            .OrderByDescending(m => m.Tank_Movement_ID)
//                            .FirstOrDefault();
//                        ticketId = previousMovement?.Tank_Ticket_ID;
//                    }

//                    Console.WriteLine($"[SetTankMovementThreshold] Creating new movement with TicketId: {ticketId}");

//                    movement = new TankMovement
//                    {
//                        Tank_Number = request.TankNumber,
//                        Status = 0,
//                        TargetLevel = targetLevel,
//                        AlarmTimeStamp = alarmTimeSpan,
//                        StagnantThresholdMinutes = stagnantMinutes,
//                        StagnantAlarm = false,
//                        TimeStamp = DateTime.Now,
//                        LastFlowrateChangeTime = DateTime.Now,
//                        Level = liveData?.Level ?? 0,
//                        Level_Water = liveData?.Level_Water ?? 0,
//                        Temperature = liveData?.Temperature ?? 0.0,
//                        Density = liveData?.Density ?? 0.0
//                    };
//                    _context.Tank_Movement.Add(movement);
//                    Console.WriteLine($"[SetTankMovementThreshold] Created new movement for {request.TankNumber}");
//                }
//                else
//                {
//                    movement.Status = true;
//                    movement.TargetLevel = targetLevel;
//                    movement.AlarmTimeStamp = alarmTimeSpan;
//                    movement.StagnantThresholdMinutes = stagnantMinutes;
//                    movement.StagnantAlarm = false;
//                    movement.TimeStamp = DateTime.Now;
//                    movement.LastFlowrateChangeTime = DateTime.Now;
//                    movement.Level = liveData?.Level ?? 0;
//                    movement.Level_Water = liveData?.Level_Water ?? 0;
//                    movement.Temperature = liveData?.Temperature ?? 0.0;
//                    movement.Density = liveData?.Density ?? 0.0;
//                    _context.Update(movement);
//                    Console.WriteLine($"[SetTankMovementThreshold] Updated movement for {request.TankNumber}");
//                }

//                _context.SaveChanges();

//                Console.WriteLine($"[SetTankMovementThreshold] ✅ Successfully saved! Status={movement.Status}, StagnantThreshold={movement.StagnantThresholdMinutes}min");

//                return Json(new
//                {
//                    isValid = true,
//                    message = $"Tank Movement monitoring berhasil diaktifkan untuk {request.TankNumber}",
//                    tankNumber = request.TankNumber,
//                    targetLevel = targetLevel,
//                    alarmMinutes = alarmMinutes,
//                    stagnantMinutes = stagnantMinutes
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[SetTankMovementThreshold] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        /// <summary>
//        /// Request model untuk SetTankMovementThreshold
//        /// </summary>
//        public class TankMovementThresholdRequest
//        {
//            public string TankNumber { get; set; }
//            public int TargetLevel { get; set; }
//            public int AlarmMinutes { get; set; }
//            public int StagnantThresholdMinutes { get; set; }
//            public long? TankTicketId { get; set; }  // ✅ Tank_Ticket_ID from ROT Open
//        }

//        // ✅ NEW: Get Settings for Alarm Configuration Modal
//        /// <summary>
//        /// Get current Tank Movement settings for the "Set Alarm" modal
//        /// Called by loadTankMovementSettings() in Index.cshtml
//        /// </summary>
//        [HttpGet]
//        public IActionResult GetSettings(string tankName)
//        {
//            try
//            {
//                Console.WriteLine($"[GetSettings] Loading settings for tank: {tankName}");

//                if (string.IsNullOrEmpty(tankName))
//                {
//                    return Json(new { isValid = false, message = "Tank name required" });
//                }

//                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankName);
//                if (tank == null)
//                {
//                    return Json(new { isValid = false, message = $"Tank {tankName} not found" });
//                }

//                // ✅ FIX: Try to find ACTIVE Tank_Movement record first
//                // If no active record → check for latest stopped record (untuk display settings)
//                var movement = _context.Tank_Movement
//                    .FirstOrDefault(m => m.Tank_Number == tankName && m.Status == 0);

//                // If no active movement, get latest stopped record (untuk display settings)
//                if (movement == null)
//                {
//                    movement = _context.Tank_Movement
//                        .Where(m => m.Tank_Number == tankName && m.Status == 0)
//                        .OrderByDescending(m => m.Tank_Movement_ID)
//                        .FirstOrDefault();
//                }

//                if (movement != null)
//                {
//                    Console.WriteLine($"[GetSettings] Found movement: ID={movement.Tank_Movement_ID}, Status={movement.Status}, IsManuallyConfigured={movement.IsManuallyConfigured}");

//                    return Json(new
//                    {
//                        isValid = true,
//                        data = new
//                        {
//                            tankName = movement.Tank_Number,
//                            targetLevel = movement.TargetLevel,
//                            stagnantThresholdMinutes = movement.StagnantThresholdMinutes ?? 5,
//                            isManuallyConfigured = movement.IsManuallyConfigured,
//                            status = movement.Status,
//                            stagnantAlarm = movement.StagnantAlarm,
//                            lastFlowrateChangeTime = movement.LastFlowrateChangeTime,
//                            tankTicketId = movement.Tank_Ticket_ID
//                        }
//                    });
//                }
//                else
//                {
//                    Console.WriteLine($"[GetSettings] No Tank_Movement record found for {tankName}");
//                    return Json(new { isValid = false, message = "No Tank Movement record found" });
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[GetSettings] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ✅ NEW: Get Tanks with Recent ROT Tickets (for Alarm Modal dropdown)
//        /// <summary>
//        /// Get tanks that have ROT OPEN tickets (Status_Reservasi = 4) within last N days
//        /// Used by the alarm configuration modal to filter only relevant tanks
//        /// </summary>
//        [HttpGet]
//        public IActionResult GetTanksWithRecentROT(int days = 3)
//        {
//            try
//            {
//                Console.WriteLine($"[GetTanksWithRecentROT] Getting tanks with ROT tickets within {days} days");

//                var cutoffDate = DateTime.Now.AddDays(-days);

//                // Get distinct tanks that have ROT OPEN tickets (Status_Reservasi = 4) within cutoff date
//                var tanksWithROT = _context.Tank_Ticket
//                    .Where(tt => tt.StatusReservasi == 4  // ROT
//                              && tt.Operation_Status == 1  // OPEN
//                              && tt.Created_Timestamp >= cutoffDate)
//                    .Select(tt => new {
//                        TankNumber = tt.Tank_Number,
//                        TicketId = tt.Id,
//                        TicketNumber = tt.Ticket_Number,
//                        CreatedAt = tt.Created_Timestamp
//                    })
//                    .OrderByDescending(x => x.CreatedAt)
//                    .ToList();

//                // Group by tank to get latest ticket per tank
//                var latestPerTank = tanksWithROT
//                    .GroupBy(x => x.TankNumber)
//                    .Select(g => g.First())
//                    .OrderBy(x => x.TankNumber)
//                    .ToList();

//                Console.WriteLine($"[GetTanksWithRecentROT] Found {latestPerTank.Count} tanks with recent ROT tickets");

//                return Json(new
//                {
//                    isValid = true,
//                    count = latestPerTank.Count,
//                    data = latestPerTank.Select(t => new {
//                        tankNumber = t.TankNumber,
//                        ticketId = t.TicketId,
//                        ticketNumber = t.TicketNumber,
//                        createdAt = t.CreatedAt?.ToString("dd MMM yyyy HH:mm")
//                    })
//                });
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[GetTanksWithRecentROT] ERROR: {ex.Message}");
//                return Json(new { isValid = false, message = ex.Message });
//            }
//        }

//        // ✅ NEW: Update Settings from Alarm Configuration Modal
//        /// <summary>
//        /// Update or create Tank Movement settings from "Set Alarm" modal
//        /// Called by saveAlarmSettings() in Index.cshtml
//        /// tankTicketId is passed when coming from ROT Open ticket creation
//        /// </summary>
//        //[HttpPost]
//        //public IActionResult UpdateSettings(string tankName, int? targetLevel, int? stagnantThresholdMinutes, bool isManuallyConfigured, long? tankTicketId = null)
//        //{
//        //    //try
//        //    //{
//        //    //    Console.WriteLine($"[UpdateSettings] Tank: {tankName}, TargetLevel: {targetLevel}, Stagnant: {stagnantThresholdMinutes}min, IsManual: {isManuallyConfigured}, TicketId: {tankTicketId}");

//        //    //    if (string.IsNullOrEmpty(tankName))
//        //    //    {
//        //    //        return Json(new { isValid = false, message = "Tank name required" });
//        //    //    }

//        //    //    var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankName);
//        //    //    if (tank == null)
//        //    //    {
//        //    //        return Json(new { isValid = false, message = $"Tank {tankName} not found" });
//        //    //    }

//        //    //    var liveData = _context.Tank_Live_Data.FirstOrDefault(l => l.Tank_Number == tankName);

//        //    //    // ✅ FIX: Try to find existing ACTIVE Tank_Movement record (Status = true)
//        //    //    // Jika tidak ada yang active → buat BARU (tidak update yang stopped)
//        //    //    var movement = _context.Tank_Movement
//        //    //        .FirstOrDefault(m => m.Tank_Number == tankName && m.Status == true);

//        //    //    if (movement != null)
//        //    //    {
//        //    //        // ✅ Update existing ACTIVE record
//        //    //        Console.WriteLine($"[UpdateSettings] Updating existing ACTIVE Tank_Movement ID={movement.Tank_Movement_ID}");

//        //    //        movement.TargetLevel = targetLevel ?? (int?)tank.Height_Safe_Capacity ?? 5000;
//        //    //        movement.StagnantThresholdMinutes = stagnantThresholdMinutes ?? 5;
//        //    //        movement.IsManuallyConfigured = isManuallyConfigured;
//        //    //        movement.TimeStamp = DateTime.Now;
//        //    //        movement.LastFlowrateChangeTime = DateTime.Now;
//        //    //        movement.StagnantAlarm = false;

//        //    //        // Update live data values
//        //    //        if (liveData != null)
//        //    //        {
//        //    //            movement.Level = liveData.Level;
//        //    //            movement.Level_Water = liveData.Level_Water;
//        //    //            movement.Temperature = liveData.Temperature;
//        //    //            movement.Density = liveData.Density;
//        //    //            movement.Volume = liveData.Volume;
//        //    //            movement.Flowrate = liveData.Flowrate;
//        //    //        }

//        //    //        _context.Update(movement);
//        //    //    }
//        //    //    else
//        //    //    {
//        //    //        // ✅ Create new Tank_Movement record
//        //    //        Console.WriteLine($"[UpdateSettings] Creating new Tank_Movement for {tankName}");

//        //    //        // ✅ FIX: Use tankTicketId from parameter if provided (from ROT Open)
//        //    //        // Otherwise, try to find latest record untuk ambil Tank_Ticket_ID
//        //    //        long? ticketId = tankTicketId;

//        //    //        if (ticketId == null)
//        //    //        {
//        //    //            var previousMovement = _context.Tank_Movement
//        //    //                .Where(m => m.Tank_Number == tankName)
//        //    //                .OrderByDescending(m => m.Tank_Movement_ID)
//        //    //                .FirstOrDefault();

                        
//        //    //            Console.WriteLine($"[UpdateSettings] Previous movement found: ID={previousMovement?.Tank_Movement_ID}, Ticket={ticketId}");
//        //    //        }
//        //    //        else
//        //    //        {
//        //    //            Console.WriteLine($"[UpdateSettings] Using tankTicketId from parameter: {ticketId}");
//        //    //        }

//        //    //        movement = new TankMovement
//        //    //        {
//        //    //            Tank_Number = tankName,
//        //    //            Status = 0,
//        //    //            TargetLevel = targetLevel ?? (int?)tank.Height_Safe_Capacity ?? 5000,
//        //    //            StagnantThresholdMinutes = stagnantThresholdMinutes ?? 5,
//        //    //            IsManuallyConfigured = isManuallyConfigured,
//        //    //            StagnantAlarm = false,
//        //    //            TimeStamp = DateTime.Now,
//        //    //            LastFlowrateChangeTime = DateTime.Now,
//        //    //            Level = liveData?.Level,
//        //    //            Level_Water = liveData?.Level_Water,
//        //    //            Temperature = liveData?.Temperature,
//        //    //            Density = liveData?.Density,
//        //    //            Volume = liveData?.Volume,
//        //    //            Flowrate = liveData?.Flowrate
//        //    //        };

//        //    //        _context.Tank_Movement.Add(movement);
//        //    //    }

//        //    //    _context.SaveChanges();

//        //    //    Console.WriteLine($"[UpdateSettings] ✅ Settings saved successfully! Tank_Movement_ID={movement.Tank_Movement_ID}");

//        //    //    return Json(new
//        //    //    {
//        //    //        isValid = true,
//        //    //        message = $"Alarm settings for {tankName} saved successfully",
//        //    //        data = new
//        //    //        {
//        //    //            tankName = movement.Tank_Number,
//        //    //            targetLevel = movement.TargetLevel,
//        //    //            stagnantThresholdMinutes = movement.StagnantThresholdMinutes,
//        //    //            isManuallyConfigured = movement.IsManuallyConfigured,
//        //    //            tankMovementId = movement.Tank_Movement_ID
//        //    //        }
//        //    //    });
//        //    //}
//        //    //catch (Exception ex)
//        //    //{
//        //    //    Console.WriteLine($"[UpdateSettings] ERROR: {ex.Message}");
//        //    //    Console.WriteLine($"[UpdateSettings] StackTrace: {ex.StackTrace}");
//        //    //    return Json(new { isValid = false, message = ex.Message });
//        //    //}
//        //}

//        // ✅ NEW: Sync Tank_Movement from Tank_Ticket (for FDM Web compatibility)
//        /// <summary>
//        /// Auto-polling logic: Detect ROT OPEN tickets without Tank_Movement and create records
//        /// This should be called periodically (e.g., every 30 seconds) from background job or _LayoutNew.cshtml
//        /// </summary>
//        [HttpGet]
//        public IActionResult SyncTankMovementsFromTickets()
//        {
//            try
//            {
//                Console.WriteLine("[SyncTankMovementsFromTickets] Starting sync...");

//                // ✅ FIX: Only sync RECENT ROT OPEN tickets (within last 7 days)
//                // Old tickets (> 7 days) should be closed manually, not monitored
//                var cutoffDate = DateTime.Now.AddDays(-7);

//                // Find all ROT OPEN tickets (Operation_Status = 1 for OPEN, StatusReservasi = 4 for ROT)
//                var openTickets = _context.Tank_Ticket
//                    .Where(t => t.Operation_Status == 1 && t.StatusReservasi == 4 && t.Timestamp >= cutoffDate)
//                    .ToList();

//                Console.WriteLine($"[SyncTankMovementsFromTickets] Found {openTickets.Count} open ROT tickets (recent < 7 days)");

//                int createdCount = 0;
//                int skippedCount = 0;

//            //    foreach (var ticket in openTickets)
//            //    {
//            //        // Check if Tank_Movement already exists for this ticket
//            //        var existingMovement = _context.Tank_Movement
//            //            .FirstOrDefault(m => m.Tank_Ticket_ID == ticket.Id);

//            //        if (existingMovement != null)
//            //        {
//            //            // Movement already exists, skip
//            //            skippedCount++;
//            //            Console.WriteLine($"[SyncTankMovementsFromTickets] Ticket {ticket.Id} already has Tank_Movement ID={existingMovement.Tank_Movement_ID}, skip");
//            //            continue;
//            //        }

//            //        // Check if there's a manually configured movement for this tank (don't overwrite)
//            //        var manualMovement = _context.Tank_Movement
//            //            .FirstOrDefault(m => m.Tank_Number == ticket.Tank_Number &&
//            //                               m.IsManuallyConfigured == true &&
//            //                               m.Status == true);

//            //        if (manualMovement != null)
//            //        {
//            //            // Manual configuration exists, skip auto-creation
//            //            skippedCount++;
//            //            Console.WriteLine($"[SyncTankMovementsFromTickets] Tank {ticket.Tank_Number} has manual config (ID={manualMovement.Tank_Movement_ID}), skip auto-creation");
//            //            continue;
//            //        }

//            //        // ✅ Create new Tank_Movement with default settings
//            //        var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == ticket.Tank_Number);
//            //        var liveData = _context.Tank_Live_Data.FirstOrDefault(l => l.Tank_Number == ticket.Tank_Number);

//            //        if (tank == null)
//            //        {
//            //            Console.WriteLine($"[SyncTankMovementsFromTickets] Tank {ticket.Tank_Number} not found, skip");
//            //            continue;
//            //        }

//            //        int defaultStagnantThreshold = _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);

//            //        var newMovement = new TankMovement
//            //        {
//            //            Tank_Number = ticket.Tank_Number,
//            //            Status = 0,
//            //            TargetLevel = (int?)tank.Height_Safe_Capacity ?? 5000,
//            //            StagnantThresholdMinutes = defaultStagnantThreshold,
//            //            IsManuallyConfigured = false, // ⚠️ IMPORTANT: Auto-created, not manual
//            //            StagnantAlarm = false,
//            //            TimeStamp = DateTime.Now,
//            //            LastFlowrateChangeTime = DateTime.Now,
//            //            Level = liveData?.Level,
//            //            Level_Water = liveData?.Level_Water,
//            //            Temperature = liveData?.Temperature,
//            //            Density = liveData?.Density,
//            //            Volume = liveData?.Volume,
//            //            Flowrate = liveData?.Flowrate
//            //        };

//            //        _context.Tank_Movement.Add(newMovement);
//            //        createdCount++;

//            //        Console.WriteLine($"[SyncTankMovementsFromTickets] ✅ Created Tank_Movement for Ticket {ticket.Id}, Tank {ticket.Tank_Number}");
//            //    }

//            //    _context.SaveChanges();

//            //    Console.WriteLine($"[SyncTankMovementsFromTickets] Sync completed! Created: {createdCount}, Skipped: {skippedCount}");

//            //    return Json(new
//            //    {
//            //        isValid = true,
//            //        message = $"Sync completed: {createdCount} created, {skippedCount} skipped",
//            //        createdCount = createdCount,
//            //        skippedCount = skippedCount
//            //    });
//            //}
//            //catch (Exception ex)
//            //{
//            //    Console.WriteLine($"[SyncTankMovementsFromTickets] ERROR: {ex.Message}");
//            //    return Json(new { isValid = false, message = ex.Message });
//            //}
//        }
//    }
//}
