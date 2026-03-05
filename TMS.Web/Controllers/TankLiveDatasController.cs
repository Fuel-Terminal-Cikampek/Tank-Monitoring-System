using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CSL.Web.Models;
using CSL.Web;
using Microsoft.AspNetCore.Authorization;
using TMS.Web.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Models;
using static Org.BouncyCastle.Asn1.Cmp.Challenge;
using static TMS.Web.Controllers.TankTicketController;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class TankLiveDatasController : Controller
    {
        private readonly TMSContext _context;
        private readonly IConfiguration _configuration;

        public TankLiveDatasController(TMSContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            populateTanks();

            // Pass polling interval to view (default 1000ms = 1 second)
            var pollingInterval = _configuration.GetValue<int>("PollingSettings:TankMonitoringPollingIntervalMs", 1000);
            ViewBag.PollingIntervalMs = pollingInterval;

            return View();
        }
        public IActionResult LoadData()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();// Skip number of Rows count  
                var length = Request.Form["length"].FirstOrDefault(); // Paging Length 10,20  
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault(); // Sort Column Name  
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();// Sort Column Direction (asc, desc)  
                var searchValue = Request.Form["search[value]"].FirstOrDefault(); // Search Value from (Search box)  
                int pageSize = length != null ? Convert.ToInt32(length) : 0; //Paging Size (10, 20, 50,100)  
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;
                //get all data
                // ✅ Use Tank_LiveDataTMS because it has legacy columns (Ack, AlarmMessage, Pumpable, Ullage)
                var tankLiveDatas = (from p in _context.Tank
                                     join t in _context.Tank_LiveDataTMS on p.Tank_Name equals t.Tank_Number
                                     join c in _context.Master_Product on p.Product_ID equals c.Product_Code
                                     join m in _context.Tank_Movement on p.Tank_Name equals m.Tank_Number into joinM
                                     from m in joinM.DefaultIfEmpty() // Left Join
                                     select new
                                     {
                                         Tank_Name = p.Tank_Name,
                                         HexColor = c.HexColor,
                                         Product_Name = c.Product_Name,
                                         // ✅ FIX: Status ALARM hanya jika Alarm_Status > 0 DAN Ack = true (belum di-acknowledge)
                                         // Jika sudah di-ACK (Ack = false), status kembali NORMAL meskipun Alarm_Status masih ada
                                         Status = ((t.Alarm_Status ?? 0) > 0 && (t.Ack ?? true) == true) ? "ALARM" : "NORMAL",
                                         AlarmType = t.Alarm_Status, // int - akan di-convert ke string setelah ToList()
                                         AlarmMessage = t.AlarmMessage,
                                         TimeStamp = t.TimeStamp,
                                         LiquidLevel = t.LiquidLevel,
                                         WaterLevel = p.IsManualWaterLevel == true ? p.ManualWaterLevel : t.WaterLevel,
                                         LiquidTemperature = p.IsManualTemp == true ? p.ManualTemp : t.LiquidTemperature,
                                         ManualTestTemp = p.IsManualTestTemp == true ? p.ManualTestTemp : 0,
                                         LiquidDensity = p.IsManualDensity == true ? p.ManualLabDensity : t.LiquidDensity,
                                         Volume = t.Volume_Obs ?? 0,  // ✅ Explicit binding to Volume_Obs from dbo.Tank_Live_Data
                                         Pumpable = t.Pumpable,
                                         Ullage = t.Ullage,
                                         FlowRateKLperH = t.FlowRateKLperH,
                                         
                                         // Operation fields
                                         MovementStatus = m != null ? m.Status : 0,
                                         TargetLevel = m != null ? m.TargetLevel : 0,
                                         LevelH = p.LevelH,
                                         LevelL = p.LevelL,
                                         SafeHeight = p.Height_Safe_Capacity
                                     });

                // ✅ FIX: ToList() SEBELUM OrderBy untuk in-memory sorting (menghindari expression tree error)
                var dataList = tankLiveDatas.ToList();

                //Sorting - sort numerically by extracting number from Tank_Name
                var sortedData = dataList
                    .OrderBy(t =>
                    {
                        // Extract numeric part from Tank_Name (e.g., "T-1" -> 1, "T-10" -> 10)
                        var parts = t.Tank_Name.Split('-');
                        if (parts.Length > 1 && int.TryParse(parts[1], out int number))
                            return number;
                        return 0;
                    })
                    .ToList();

                //Search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    sortedData = sortedData.Where(m => m.Tank_Name.Contains(searchValue)).ToList();
                }

                //total number of rows counts
                recordsTotal = sortedData.Count;
                //Paging
                var data = sortedData.Skip(skip).Take(pageSize).ToList();

                // Convert to explicit property order to ensure consistency
                var orderedData = data.Select((item, index) =>
                {
                    // Infer Operation Type
                    string operationLabel = "IDLE"; // Default (0)
                    
                    if (item.MovementStatus == 1) operationLabel = "RECEIVING";
                    else if (item.MovementStatus == 2) operationLabel = "SALES";
                    else if (item.MovementStatus > 2) operationLabel = "OPEN"; // Fallback for other status if any

                    var dict = new Dictionary<string, object>
                    {
                        ["Tank_Name"] = item.Tank_Name,
                        ["HexColor"] = item.HexColor,
                        ["Product_Name"] = item.Product_Name,
                        ["LiquidLevel"] = item.LiquidLevel,
                        ["WaterLevel"] = item.WaterLevel,
                        ["LiquidTemperature"] = item.LiquidTemperature,
                        ["ManualTestTemp"] = item.ManualTestTemp,
                        ["LiquidDensity"] = item.LiquidDensity,
                        ["Volume"] = item.Volume,
                        ["Pumpable"] = item.Pumpable,
                        ["Ullage"] = item.Ullage,
                        ["FlowRateKLperH"] = item.FlowRateKLperH,
                        ["Status"] = item.Status,
                        ["AlarmType"] = GetAlarmTypeName(item.AlarmType), // Convert int? to string
                        ["AlarmMessage"] = item.AlarmMessage,
                        ["TimeStamp"] = item.TimeStamp,
                        ["OperationLabel"] = operationLabel // ✅ New field
                    };
                    
                    if (index < 3)
                    {
                        Console.WriteLine($"[BACKEND LOG] Tank {item.Tank_Name}: Op={operationLabel}, Status={item.MovementStatus}, Target={item.TargetLevel}");
                    }

                    return dict;
                }).ToList();

                //Returning Json Data - Use Content to ensure proper JSON serialization
                var jsonResult = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = orderedData },
                    Newtonsoft.Json.Formatting.None,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                    });

                // LOG JSON OUTPUT for first data item
                if (orderedData.Count > 0)
                {
                    var firstItem = orderedData[0];
                    Console.WriteLine("====================================");
                    Console.WriteLine("[JSON SERIALIZED - First Item]");
                    Console.WriteLine($"JSON Substring (first 500 chars): {jsonResult.Substring(0, Math.Min(500, jsonResult.Length))}");
                    Console.WriteLine("====================================");
                }

                return Content(jsonResult, "application/json");
            }
            catch (Exception)
            {
                throw;
            }
        }
        // GET: Tanks/AddOrEdit
        [NoDirectAccess]
        public IActionResult CalculationView()
        {
            // ✅ FIX: Update calculation di database terlebih dahulu
            CalculateEstimationForAllActiveTanks();

            // Pass polling interval dari appsettings.json ke view
            var pollingInterval = _configuration.GetValue<int>("PollingSettings:TankMonitoringPollingIntervalMs", 1000);
            ViewBag.PollingIntervalMs = pollingInterval;

            // Pass alarm polling interval
            var alarmPollingInterval = _configuration.GetValue<int>("PollingSettings:TankMovementAlarmPollingIntervalMs", 3000);
            ViewBag.TankMovementAlarmPollingMs = alarmPollingInterval;

            populateTanks();

            // ✅ Load only tanks with active movement (filter BEFORE loading data)
            var activeTankNames = _context.Tank_Movement
                .Where(m => m.Status == 0)
                .Select(m => m.Tank_Number)
                .ToList();

            var tanks = _context.Tank
                .Where(t => activeTankNames.Contains(t.Tank_Name))
                .ToList();

            if (tanks != null && tanks.Any())
            {
                Console.WriteLine($"[CalculationView] Loading {tanks.Count} active tanks");

                foreach (var item in tanks)
                {
                    // Load TankLiveDataTMS for access to legacy columns like TotalSecond
                    // Note: Using Tank_LiveDataTMS because GetHybridFlowrate needs TotalSecond (legacy column)
                    var live = _context.Tank_LiveDataTMS.FirstOrDefault(i => i.Tank_Number == item.Tank_Name);

                    Console.WriteLine($"[CalculationView] Tank {item.Tank_Name}: LiveData {(live == null ? "NULL" : $"Level={live.Level}")}");

                    if (live != null)
                    {
                        // ✅ FIX: DON'T manipulate Level for Tank Movement!
                        // Tank Movement needs REAL-TIME Level from sensor, not manual override
                        // LiquidLevel setter OVERWRITES Level property!

                        // ⚠️ TODO: Type mismatch - Tank.tankLiveData expects TankLiveData but we're using TankLiveDataTMS
                        // If this is needed for the view, consider changing Tank model or creating a conversion method
                        // item.tankLiveData = live;  // COMMENTED OUT - type mismatch

                        Console.WriteLine($"[CalculationView] Tank {item.Tank_Name}: LiveData loaded, Level={live.Level}");
                    }
                    else
                    {
                        Console.WriteLine($"[CalculationView] WARNING: Tank {item.Tank_Name} has NO LiveData!");
                    }

                    // ✅ Load Tank_Movement dari DB (sudah di-update oleh CalculateEstimationForAllActiveTanks)
                    var movement = _context.Tank_Movement.FirstOrDefault(i => i.Tank_Number == item.Tank_Name);
                    if (movement != null)
                    {
                        item.tankMovement = movement;

                        // ✅ Calculate hybrid flowrate (manual < 6 min, sensor >= 6 min)
                        if (live != null)
                        {
                            item.HybridFlowrate = GetHybridFlowrate(live, movement, item);
                        }
                    }
                }
            }
            else
            {
                return View(new List<Tank>());
            }

            // ✅ Return same list (no new ToList() that loses assignments!)
            Console.WriteLine($"[CalculationView] Returning {tanks.Count} tanks to view:");
            foreach (var t in tanks)
            {
                Console.WriteLine($"  - {t.Tank_Name}: tankLiveData={(t.tankLiveData == null ? "NULL" : $"OK(Level={t.tankLiveData.Level})")}, HybridFlowrate={t.HybridFlowrate:F2}");
            }

            return View(tanks);
        }

        // ✅ NEW: Calculate estimation untuk semua tank yang aktif monitoring
        private void CalculateEstimationForAllActiveTanks()
        {
            var movements = _context.Tank_Movement.Where(m => m.Status == 0).ToList();

            foreach (var movement in movements)
            {
                var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == movement.Tank_Number);
                // ✅ Use Tank_LiveDataTMS because GetHybridFlowrate needs TotalSecond (legacy column)
                var liveData = _context.Tank_LiveDataTMS.FirstOrDefault(l => l.Tank_Number == movement.Tank_Number);

                if (tank != null && liveData != null)
                {
                    // Gunakan TargetLevel dari Tank_Movement (bukan Height_Safe_Capacity)
                    var targetLevel = movement.TargetLevel ?? tank.Height_Safe_Capacity;

                    // ✅ HYBRID: Use hybrid flowrate (manual < 6 min, sensor >= 6 min)
                    var flowrateKLperH = GetHybridFlowrate(liveData, movement, tank);
                    var flowrateMperS = ConvertFlowrateToMeterPerSecond(flowrateKLperH, tank.RaisePerMM);

                    // ✅ STAGNANT ALARM DETECTION (use sensor flowrate for stagnant detection)
                    DetectStagnantCondition(movement, liveData.Flowrate ?? 0);

                    // Calculate estimation
                    if (flowrateMperS > 0)
                    {
                        var deltaLevel = targetLevel - (liveData.Level ?? 0);
                        var seconds = deltaLevel / (flowrateMperS * 1000); // m/s to mm/s
                        movement.EstimationTimeStamp = TimeSpan.FromSeconds(Math.Abs(seconds));

                        // Update alarm status
                        if (movement.EstimationTimeStamp <= movement.AlarmTimeStamp)
                        {
                            movement.Alarm = true;
                        }
                        else
                        {
                            movement.Alarm = false;
                        }
                    }
                    else
                    {
                        movement.EstimationTimeStamp = null;
                        movement.Alarm = false;
                    }

                    _context.Update(movement);
                }
            }

            _context.SaveChanges();
        }

        // ✅ NEW: Detect stagnant condition (no flowrate during active monitoring)
        private void DetectStagnantCondition(TankMovement movement, double currentFlowrate)
        {
            // Read threshold from appsettings.json, fallback to DB value, then default to 5 minutes
            int configThreshold = _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);
            int thresholdMinutes = movement.StagnantThresholdMinutes ?? configThreshold;
            double flowrateThreshold = 0.01; // KL/h - consider as "no flow" if below this

            // Check if flowrate is stagnant (near zero)
            bool isStagnant = Math.Abs(currentFlowrate) < flowrateThreshold;

            if (isStagnant)
            {
                // Initialize LastFlowrateChangeTime if null
                if (movement.LastFlowrateChangeTime == null)
                {
                    movement.LastFlowrateChangeTime = DateTime.Now;
                    movement.StagnantAlarm = false;
                }
                else
                {
                    // Check if stagnant duration exceeds threshold
                    var stagnantDuration = DateTime.Now - movement.LastFlowrateChangeTime.Value;
                    if (stagnantDuration.TotalMinutes >= thresholdMinutes)
                    {
                        movement.StagnantAlarm = true;
                    }
                }
            }
            else
            {
                // Flowrate is active, reset stagnant tracking
                movement.LastFlowrateChangeTime = DateTime.Now;
                movement.StagnantAlarm = false;
            }
        }

        // ✅ NEW: Convert Flowrate dari KL/h ke meter/second
        private double ConvertFlowrateToMeterPerSecond(double flowrateKLperH, double raisePerMM)
        {
            if (flowrateKLperH == 0 || raisePerMM == 0) return 0;

            // Flowrate KL/h → Liter/h → Liter/s → mm/s → m/s
            var literPerHour = flowrateKLperH * 1000;  // KL to L
            var literPerSecond = literPerHour / 3600;   // per hour to per second
            var mmPerSecond = literPerSecond / raisePerMM;  // Liter to mm
            var meterPerSecond = mmPerSecond / 1000;    // mm to meter

            return meterPerSecond;
        }

        // ✅ NEW: Hybrid Flowrate Calculation (Manual < 6 min, Sensor >= 6 min)
        // ✅ Uses TankLiveDataTMS because it needs TotalSecond (legacy column)
        private double GetHybridFlowrate(TankLiveDataTMS liveData, TankMovement movement, Tank tank)
        {
            if (liveData == null || movement == null || tank == null) return 0;

            var totalSecond = liveData.TotalSecond ?? 0;

            // Threshold: 6 minutes = 360 seconds
            if (totalSecond < 360)
            {
                // < 6 minutes: Manual calculation from snapshot
                if (!movement.TimeStamp.HasValue || !movement.Level.HasValue) return 0;

                var deltaLevel = (liveData.Level ?? 0) - movement.Level.Value;  // mm
                var deltaTime = (DateTime.Now - movement.TimeStamp.Value).TotalSeconds;  // seconds

                if (deltaTime <= 0) return 0;

                // Calculate flowrate: mm/s -> KL/h
                var mmPerSecond = deltaLevel / deltaTime;
                var flowrateKLperH = mmPerSecond * tank.RaisePerMM * 3.6;  // mm/s * L/mm * 3600s/1000L = KL/h

                return flowrateKLperH;
            }
            else
            {
                // >= 6 minutes: Sensor flowrate (more accurate, already stable)
                return liveData.Flowrate ?? 0;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> RequestEstimation(RequestEstimated request)
        {
            string message = "";
            if (request == null)
            {
                return Ok("Please Check Request Time");
            }
            var tank = await _context.Tank.FindAsync(request.Tank_Name);
            if (tank == null)
            {
                return Ok("Please Check Tank Request to Time estimation");
            }
            else
            {
                // ✅ FIX: Jangan overwrite Height_Safe_Capacity (itu master data!)
                // Tank.Height_Safe_Capacity tetap tidak berubah
                var moveMent = await _context.Tank_Movement.FindAsync(tank.Tank_Name);  // Use Tank_Name not TankId

                // ✅ GET CURRENT LIVE DATA untuk snapshot
                var liveData = await _context.Tank_Live_Data
                    .FirstOrDefaultAsync(t => t.Tank_Number == tank.Tank_Name);

                if (moveMent != null)
                {
                    moveMent.Status = request.Status;
                    moveMent.AlarmTimeStamp = request.Time;
                    moveMent.TargetLevel = (int)request.Height;  // ✅ Simpan target ke Tank_Movement (bukan Tank master)

                    // ✅ UPDATE SNAPSHOT DATA dari LiveData saat ini
                    if (liveData != null)
                    {
                        moveMent.TimeStamp = liveData.TimeStamp ?? DateTime.Now;
                        moveMent.Level = liveData.Level;
                        moveMent.Level_Water = liveData.Level_Water;
                        moveMent.Temperature = liveData.Temperature;
                        moveMent.Density = liveData.Density;
                        moveMent.Volume = liveData.Volume_Obs;
                        moveMent.Flowrate = liveData.Flowrate;
                    }

                    // ✅ INITIALIZE STAGNANT ALARM TRACKING
                    // Always set threshold if NULL (regardless of status)
                    Console.WriteLine($"[RequestEstimation] Tank {tank.Tank_Name}: StagnantThresholdMinutes BEFORE = {moveMent.StagnantThresholdMinutes}");

                    if (moveMent.StagnantThresholdMinutes == null)
                    {
                        int configThreshold = _configuration.GetValue<int>("PollingSettings:StagnantThresholdMinutes", 5);
                        moveMent.StagnantThresholdMinutes = configThreshold;
                        Console.WriteLine($"[RequestEstimation] Tank {tank.Tank_Name}: Setting StagnantThresholdMinutes = {configThreshold}");
                    }

                    Console.WriteLine($"[RequestEstimation] Tank {tank.Tank_Name}: StagnantThresholdMinutes AFTER = {moveMent.StagnantThresholdMinutes}");

                    Console.WriteLine($"[RequestEstimation] Tank {tank.Tank_Name}: StagnantThresholdMinutes AFTER = {moveMent.StagnantThresholdMinutes}");

                    if (request.Status > 0)
                    {
                        // Opening monitoring
                        moveMent.LastFlowrateChangeTime = DateTime.Now;
                        moveMent.StagnantAlarm = false;
                    }
                    else
                    {
                        // Closing monitoring - reset stagnant alarm
                        moveMent.StagnantAlarm = false;
                        moveMent.LastFlowrateChangeTime = null;
                    }

                    _context.Update(moveMent);
                    // ✅ REMOVED: Jangan update tank (master data tidak berubah)
                    Console.WriteLine($"[RequestEstimation] Saving to database...");
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[RequestEstimation] SAVED! StagnantThresholdMinutes in DB should be = {moveMent.StagnantThresholdMinutes}");

                    if (moveMent.Status == 0)
                    {
                        message = string.Format("Success Close Time Estimation Tank :{0}", tank.Tank_Name);
                    }
                    else
                    {
                        message = string.Format("Success Open Time Estimation Tank :{0}", tank.Tank_Name);
                    }
                }
                else
                {
                    // Tank_Movement record not found - create new one
                    var newMovement = new TankMovement
                    {
                        Tank_Number = tank.Tank_Name,
                        Status = request.Status,
                        AlarmTimeStamp = request.Time,
                        TargetLevel = (int)request.Height,  // ✅ Simpan target ke Tank_Movement
                        // ✅ SNAPSHOT DATA dari LiveData saat ini
                        TimeStamp = liveData?.TimeStamp ?? DateTime.Now,
                        Level = liveData?.Level,
                        Level_Water = liveData?.Level_Water,
                        Temperature = liveData?.Temperature,
                        Density = liveData?.Density,
                        Volume = liveData?.Volume_Obs,
                        Flowrate = liveData?.Flowrate
                    };
                    _context.Tank_Movement.Add(newMovement);
                    // ✅ REMOVED: Jangan update tank (master data tidak berubah)
                    await _context.SaveChangesAsync();

                    message = string.Format("Success Create Time Estimation for Tank :{0} (Tank_Movement record was created)", tank.Tank_Name);
                }

            }
            return Ok(message);
        }

        // Helper method untuk convert alarm status int ke string
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

        private void populateTanks(object SelectList = null)
        {
            List<Tank> tanks = new List<Tank>();
            tanks = (from t in _context.Tank select t).ToList();
            var tankip = new Tank()
            {
                Tank_Name = "---- Select Tank ----"
            };
            tanks.Insert(0, tankip);
            ViewBag.TankId = tanks;
        }
        private void OperationTypeList(object SelectList = null)
        {
            List<TankSafetyOperation> operations = new List<TankSafetyOperation>();
            var select = new TankSafetyOperation() { OperationType = "--- Select Operation ---" };
            var receiving = new TankSafetyOperation() { OperationType = "Receiving" };
            var sales = new TankSafetyOperation() { OperationType = "Sales" };
            var iddle = new TankSafetyOperation() { OperationType = "Idle" };
            operations.Add(select);
            operations.Add(receiving);
            operations.Add(sales);
            operations.Add(iddle);
            ViewBag.Operation = operations;


        }

        //get: tankticket/addoredit
        [NoDirectAccess]

        public async Task<IActionResult> AddTankTicket(string id = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                populateTanks();
                populateTankTicketType();
                return View(new NewTankTicket());
            }

            else
            {
                populateTanks();
                populateTankTicketType();
                var tank = await _context.Tank.FirstOrDefaultAsync(t => t.Tank_Name == id);
                if (tank == null)
                {
                    return NotFound();
                }
                var newTankTicket = new NewTankTicket();
                newTankTicket.Tank_Number = id;
                newTankTicket.Tank = tank;

                return View(newTankTicket);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="SelectList"></param>
        private void populateTankTicketType(object SelectList = null)
        {
            List<TicketType> ticketTypes = new List<TicketType>();
            ticketTypes.Add(new TicketType() { Operation = "--All Operation Type--", Description = "--All Operation Type--" });
            ticketTypes.Add(new TicketType() { Operation = "BLD", Description = "(BLD) BLENDING" });
            ticketTypes.Add(new TicketType() { Operation = "DL", Description = "(DL) IDLE MODE" });
            ticketTypes.Add(new TicketType() { Operation = "ICR", Description = "(ICR) ISSUE CRUDE TO PROD (REF)" });
            ticketTypes.Add(new TicketType() { Operation = "ILS", Description = "(ILS) ISSUE TO LSTK" });
            ticketTypes.Add(new TicketType() { Operation = "IOT", Description = "(IOT) ISSUE OTHER" });
            ticketTypes.Add(new TicketType() { Operation = "IS", Description = "(IS) ISSUE TO SALES" });
            ticketTypes.Add(new TicketType() { Operation = "IST", Description = "(IS) ISSUE TO STOCK TRANSFER" });
            ticketTypes.Add(new TicketType() { Operation = "OWN", Description = "(OWN) OWN USE" });
            ticketTypes.Add(new TicketType() { Operation = "PI", Description = "(PI) PHYSICAL INVENTORY" });
            ticketTypes.Add(new TicketType() { Operation = "RCR", Description = "(RCR) CRUDE RECEIPT (PTEP/REF)" });
            ticketTypes.Add(new TicketType() { Operation = "ROT", Description = "(ROT) RECEIPT OTHERS" });
            ticketTypes.Add(new TicketType() { Operation = "RPR", Description = "(RPR) PRODUCTION RECEIPT(REF)" });
            ticketTypes.Add(new TicketType() { Operation = "TPL", Description = "(TPL) PIPELINE TRANSFER" });
            ticketTypes.Add(new TicketType() { Operation = "TRF", Description = "(TRF) TRANSFER OTHERS" });
            ticketTypes.Add(new TicketType() { Operation = "TT", Description = "(TT) TANK TO TANK TRANSFER" });
            ticketTypes.Add(new TicketType() { Operation = "UDG", Description = "(UDG) UP/DOWN GRADATION" });
            ticketTypes.Add(new TicketType() { Operation = "CHK", Description = "(CHK) STOCK CHECKING" });

            //var Operation = new List<string> { "--All Operation Type--", "BLD", "DL", "ICR", "ILS", "IOT", "IS", "IST", "OWN", "PI", "CHK", "RCR", "ROT", "RPR", "TPL", "TRF", "TT" };
            ViewBag.ticketTypes = ticketTypes;//ticketTypes.Select(o => new SelectListItem { Text = o, Value = o });
        }

        // GET: GetOperationalStatus
        // ✅ UPDATE: GetOperationalStatus - Add CanDisableAlarm flag
        public IActionResult GetOperationalStatus(string tankName)
        {
            var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankName);
            if (tank == null) return NotFound();

            var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == tankName);

            string operationType = "";
            if (movement != null)
            {
                operationType = movement.Status switch
                {
                    1 => "RECEIVING",
                    2 => "SALES",
                    0 => "STANDBY",
                    _ => ""
                };
            }

            // ✅ NEW: Check if tank can disable alarm
            bool canDisableAlarm = true;

            var model = new TankSafetyOperation
            {
                TankName = tank.Tank_Name,
                // ✅ FIX: Explicit cast dari double? ke double dengan null-coalescing
                MaxLevel = (tank.LevelH ?? 0) > 0 ? (tank.LevelH ?? 0) : tank.Height_Safe_Capacity,
                MinLevel = tank.LevelL ?? 0,
                OperationType = operationType,
                TargetLevel = movement?.TargetLevel,
                StagnantThresholdMinutes = movement?.StagnantThresholdMinutes,
                DisableOperationAlarm = movement?.DisableOperationAlarm ?? false,
                Desc = ""
            };

            return PartialView("_OperationalStatus", model);
        }

        // POST: SaveOperationalStatus
        // ✅ UPDATE: SaveOperationalStatus - Handle DisableOperationAlarm
        [HttpPost]
        public async Task<IActionResult> SaveOperationalStatus(TankSafetyOperation model, int TargetLevel, int? StagnantThresholdMinutes, bool DisableOperationAlarm = false)
        {
            try
            {
                var tankMovement = await _context.Tank_Movement.FirstOrDefaultAsync(m => m.Tank_Number == model.TankName);
                var tank = await _context.Tank.FirstOrDefaultAsync(t => t.Tank_Name == model.TankName);

                if (tankMovement == null)
                {
                    tankMovement = new TankMovement
                    {
                        Tank_Number = model.TankName,
                        AlarmTimeStamp = DateTime.Now.TimeOfDay // Default
                    };
                    _context.Tank_Movement.Add(tankMovement);
                }

                // Logic based on OperationType
                if (model.OperationType == "RECEIVING")
                {
                    tankMovement.Status = 1; // RECEIVING
                    tankMovement.TargetLevel = TargetLevel; // ✅ Target Level untuk RECEIVING (High)
                    tankMovement.StagnantThresholdMinutes = StagnantThresholdMinutes;
                }
                else if (model.OperationType == "SALES")
                {
                    tankMovement.Status = 2; // SALES
                    tankMovement.TargetLevel = TargetLevel; // ✅ FIX: Target Level untuk SALES (Low)
                    // SALES tidak menggunakan StagnantThresholdMinutes
                }
                else if (model.OperationType == "STANDBY")
                {
                    tankMovement.Status = 0; // STANDBY
                    // ✅ STANDBY tidak clear TargetLevel, biarkan tersimpan untuk operation berikutnya
                }
                else
                {
                    return Json(new { success = false, message = "Invalid Operation Type" });
                }           
                
                tankMovement.DisableOperationAlarm = DisableOperationAlarm;
                

                // Update common fields
                tankMovement.TimeStamp = DateTime.Now;

                // Reset alarm flags saat status berubah
                tankMovement.StagnantAlarm = false;
                tankMovement.OperationAlarmAck = false;  // ✅ Reset ACK agar alarm bisa trigger lagi
                tankMovement.LastFlowrateChangeTime = DateTime.Now;


                // ✅ Snapshot current level untuk perbandingan alarm STANDBY
                var liveData = await _context.Tank_Live_Data.FirstOrDefaultAsync(l => l.Tank_Number == model.TankName);
                if (liveData != null)
                {
                    tankMovement.Level = liveData.Level;
                }

                await _context.SaveChangesAsync();

                string alarmStatusMsg = DisableOperationAlarm 
                    ? " (Operation Alarm DISABLED)" 
                    : "";

                return Json(new { success = true, message = $"Successfully updated status for {model.TankName} to {model.OperationType}{alarmStatusMsg}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// GET: Load Alarm Settings from appsettings.json
        /// </summary>
        [HttpGet]
        public IActionResult GetAlarmSettings(string tankName = null)
        {
            var model = new AlarmSettingsViewModel
            {
                StagnantFlowrateToleranceKLH = _configuration.GetValue<double>("AlarmSettings:StagnantFlowrateToleranceKLH", 0.05),
                DefaultStagnantThresholdMinutes = _configuration.GetValue<int>("AlarmSettings:DefaultStagnantThresholdMinutes", 5),
                StandbyFlowrateToleranceKLH = _configuration.GetValue<double>("AlarmSettings:StandbyFlowrateToleranceKLH", 20.0),
                StandbyLevelChangeToleranceMM = _configuration.GetValue<int>("AlarmSettings:StandbyLevelChangeToleranceMM", 10),
                SalesFlowrateToleranceKLH = _configuration.GetValue<double>("AlarmSettings:SalesFlowrateToleranceKLH", 20.0),
                SalesLevelChangeToleranceMM = _configuration.GetValue<int>("AlarmSettings:SalesLevelChangeToleranceMM", 5),
                LevelFluctuationToleranceMM = _configuration.GetValue<int>("AlarmSettings:LevelFluctuationToleranceMM", 2),
                CanEdit = User.IsInRole("Admin") || User.IsInRole("Administrator")
            };
            
            // ✅ Pass tankName ke ViewBag untuk back navigation
            ViewBag.TankName = tankName;
            
            return PartialView("_AlarmSettings", model);
        }

        /// <summary>
        /// POST: Save Alarm Settings to appsettings.json (Admin Only)
        /// </summary>
        [HttpPost]
        public IActionResult SaveAlarmSettings([FromBody] AlarmSettingsViewModel model)
        {
            try
            {
                // Check if user is Admin
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                bool isAdmin = RoleConstants.IsAdminRole(role);

                if (!isAdmin)
                {
                    return Json(new { success = false, message = "Unauthorized: Only Admin can change alarm settings" });
                }

                // Path to appsettings.json
                var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                
                // Read current appsettings.json as dynamic object using Newtonsoft.Json
                var json = System.IO.File.ReadAllText(appSettingsPath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                // Update AlarmSettings section
                jsonObj["AlarmSettings"]["StagnantFlowrateToleranceKLH"] = model.StagnantFlowrateToleranceKLH;
                jsonObj["AlarmSettings"]["DefaultStagnantThresholdMinutes"] = model.DefaultStagnantThresholdMinutes;
                jsonObj["AlarmSettings"]["StandbyFlowrateToleranceKLH"] = model.StandbyFlowrateToleranceKLH;
                jsonObj["AlarmSettings"]["StandbyLevelChangeToleranceMM"] = model.StandbyLevelChangeToleranceMM;
                jsonObj["AlarmSettings"]["SalesFlowrateToleranceKLH"] = model.SalesFlowrateToleranceKLH;
                jsonObj["AlarmSettings"]["SalesLevelChangeToleranceMM"] = model.SalesLevelChangeToleranceMM;
                jsonObj["AlarmSettings"]["LevelFluctuationToleranceMM"] = model.LevelFluctuationToleranceMM;

                // Serialize back to JSON with formatting
                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                
                // Write back to file
                System.IO.File.WriteAllText(appSettingsPath, output);

                // Log the change
                var username = User.Identity?.Name ?? "Unknown";
                Console.WriteLine($"[ALARM SETTINGS] Updated by {username} at {DateTime.Now}");
                Console.WriteLine($"[ALARM SETTINGS] Values: StagnantFlowrate={model.StagnantFlowrateToleranceKLH}, StandbyFlowrate={model.StandbyFlowrateToleranceKLH}");

                return Json(new { 
                    success = true, 
                    message = "Settings saved successfully! Changes will take effect on next request." 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ALARM SETTINGS] ERROR: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}

