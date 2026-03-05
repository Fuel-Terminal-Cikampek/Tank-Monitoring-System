using CSL.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Linq.Dynamic.Core;
using TMS.Web.Models;
using System.Collections.Generic;
using System;
using TMS.Web.Areas.Identity.Data;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace CSL.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TMSContext _context;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, TMSContext context, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            InitialHome();

            // Pass polling interval to view (default 1000ms = 1 second)
            var pollingInterval = _configuration.GetValue<int>("PollingSettings:DashboardPollingIntervalMs", 1000);
            ViewBag.PollingIntervalMs = pollingInterval;

            return View();
        }

        public IActionResult GetData()
        {
            try
            {
                var tanks = _context.Tank.ToList();
                var products = _context.Master_Product.ToList();
                var liveData = _context.Tank_Live_Data.ToList();
                var movements = _context.Tank_Movement.ToList();

                _logger.LogInformation("=== GetData() Debug START ===");
                _logger.LogInformation($"Tanks loaded: {tanks.Count}");
                _logger.LogInformation($"Products loaded: {products.Count}");
                _logger.LogInformation($"LiveData loaded: {liveData.Count}");

                if (liveData.Count > 0)
                {
                    var sample = liveData.First();
                    _logger.LogInformation($"Sample LiveData - Tank: {sample.Tank_Number}, Level: {sample.Level}, Temp: {sample.Temperature}, Volume: {sample.Volume_Obs}");
                }

                // Join Tank -> Product using Product_Code (Tank.Product_ID == Product.Product_Code)
                var result = tanks
                    .SelectMany(t => products.Where(p => p.Product_Code == t.Product_ID),
                        (t, p) => new
                        {
                            tank = t,
                            product = p,
                            tankId = t.Tank_Name ?? "Unknown",
                            name = t.Tank_Name ?? "Unknown",
                            productName = p?.Product_Name ?? "Unknown",
                            liveDataForTank = liveData.FirstOrDefault(ld => ld.Tank_Number == t.Tank_Name),
                            movementForTank = movements.FirstOrDefault(m => m.Tank_Number == t.Tank_Name)
                        })
                    .Select(x => new
                    {
                        tankId = x.tankId,
                        name = x.name,
                        productName = x.productName,
                        liquidLevel = x.liveDataForTank != null
                            ? String.Format("{0:N0}", x.liveDataForTank.Level ?? 0).Replace(".", ",")
                            : "0",
                        waterLevel = x.liveDataForTank != null
                            ? String.Format("{0:N0}", x.liveDataForTank.Level_Water ?? 0).Replace(".", ",")
                            : "0",
                        liquidTemperature = x.liveDataForTank?.Temperature ?? 0,
                        liquidDensity = x.liveDataForTank?.Density ?? 0,
                        volume = x.liveDataForTank != null
                            ? String.Format("{0:N0}", x.liveDataForTank.Volume_Obs ?? 0).Replace(".", ",")
                            : "0",
                        flowrate = x.liveDataForTank?.Flowrate ?? 0,
                        timeStamp = x.liveDataForTank?.TimeStamp,
                        // ✅ FIX: Validate alarm against Tank Configuration
                        // Alarm status hanya valid jika UseAlarm untuk level tersebut = true
                        status = IsAlarmValid(x.tank, x.liveDataForTank) ? "ALARM" : "NORMAL",
                        alarmType = IsAlarmValid(x.tank, x.liveDataForTank) 
                            ? GetAlarmTypeName(x.liveDataForTank?.Alarm_Status) 
                            : null,
                        alarmMessage = IsAlarmValid(x.tank, x.liveDataForTank) 
                            ? x.liveDataForTank?.AlarmMessage 
                            : null,
                        // ✅ ADD: AlarmStatus for bell icon sync
                        alarmStatus = IsAlarmValid(x.tank, x.liveDataForTank) 
                            ? x.liveDataForTank?.Alarm_Status 
                            : null,
                        persLevel = Math.Round((x.tank.Height_Safe_Capacity != 0 ? (double)(x.liveDataForTank?.Level ?? 0) / (double)x.tank.Height_Safe_Capacity * 100 : 0), 1),
                        hexColor = x.product?.HexColor ?? "#6c757d",
                        operationStatus = x.movementForTank != null ? x.movementForTank.Status : 0,
                        // ✅ ADD: maxHeight for water level percentage calculation
                        maxHeight = x.tank.Height_Safe_Capacity
                    })
                    .ToList();

                _logger.LogInformation($"Result count: {result.Count}");
                if (result.Count > 0)
                {
                    var first = result.First();
                    _logger.LogInformation($"First result - Tank: {first.name}, Product: {first.productName}, Level: {first.liquidLevel}, Volume: {first.volume}");
                }
                _logger.LogInformation("=== GetData() Debug END ===");

                return Json(result, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetData()");
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ✅ NEW: Validate if alarm is valid based on Tank Configuration
        /// Returns true only if:
        /// 1. Alarm_Status > 0
        /// 2. Ack = true (not yet acknowledged)
        /// 3. UseAlarm for that level is enabled in Tank Configuration
        /// </summary>
        private bool IsAlarmValid(Tank tank, TankLiveData liveData)
        {
            if (liveData == null) return false;
            if ((liveData.Alarm_Status ?? 0) <= 0) return false;
            if ((liveData.Ack ?? true) == false) return false; // Already ACKed

            // Check if UseAlarm is enabled for this alarm type
            int alarmStatus = liveData.Alarm_Status ?? 0;
            
            switch (alarmStatus)
            {
                case 1: // LL
                    return tank.UseAlarmLL ?? false;
                case 2: // L
                    return tank.UseAlarmL ?? false;
                case 3: // H
                    return tank.UseAlarmH ?? false;
                case 4: // HH
                    return tank.UseAlarmHH ?? false;
                case 5: // TempL
                    return tank.UseAlarmTempL ?? false;
                case 6: // TempH
                    return tank.UseAlarmTempH ?? false;
                default:
                    return false;
            }
        }

        public IActionResult GetDataByTankId(string Id)
        {
            try
            {
                var tanks = _context.Tank.ToList();
                var products = _context.Master_Product.ToList();
                var liveData = _context.Tank_Live_Data.ToList();

                var tank = tanks.FirstOrDefault(t => t.Tank_Name == Id);
                if (tank == null)
                {
                    return Json((object)null);
                }

                var product = products.FirstOrDefault(p => p.Product_Code == tank.Product_ID);
                var liveDataForTank = liveData.FirstOrDefault(ld => ld.Tank_Number == tank.Tank_Name);

                // ✅ FIX: Validate alarm against Tank Configuration
                bool hasValidAlarm = IsAlarmValid(tank, liveDataForTank);

                var result = new
                {
                    tankName = tank.Tank_Name ?? "Unknown",
                    productName = product?.Product_Name ?? "Unknown",
                    liquidLevel = liveDataForTank != null
                        ? String.Format("{0:N0}", liveDataForTank.Level ?? 0).Replace(".", ",")
                        : "0",
                    waterLevel = liveDataForTank != null
                        ? String.Format("{0:N0}", liveDataForTank.Level_Water ?? 0).Replace(".", ",")
                        : "0",
                    liquidTemperature = (tank.IsManualTemp ?? false) == false
                        ? Math.Round(liveDataForTank?.Temperature ?? 0, 2)
                        : Math.Round(tank.ManualTemp ?? 0, 2),
                    volume = Math.Round(liveDataForTank?.Volume_Obs ?? 0, 2),
                    liquidDensity = (tank.IsManualDensity ?? false) == false
                        ? Math.Round(liveDataForTank?.Density ?? 0, 3)
                        : Math.Round(tank.ManualLabDensity ?? 0, 3),
                    timeStamp = liveDataForTank?.TimeStamp,
                    // ✅ FIX: Validate alarm
                    status = hasValidAlarm ? "ALARM" : ((tank.IsUsed ?? false) ? "NORMAL" : "INACTIVE"),
                    alarmType = hasValidAlarm ? GetAlarmTypeName(liveDataForTank?.Alarm_Status) : null,
                    alarmMessage = hasValidAlarm ? liveDataForTank?.AlarmMessage : null,
                    flowrate = Math.Round(liveDataForTank?.Flowrate ?? 0, 2),
                    persLevel = Math.Round((tank.Height_Safe_Capacity != 0 ? (double)(liveDataForTank?.Level ?? 0) / (double)tank.Height_Safe_Capacity * 100 : 0), 1),
                    persVolume = Math.Round((tank.TankVolume ?? 0) != 0
                        ? (liveDataForTank?.Volume_Obs ?? 0) / (tank.TankVolume ?? 0) * 100
                        : 0, 1),
                    maxHeight = tank.Height_Safe_Capacity
                };

                return Json(result, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDataByTankId()");
                return Json(new { error = ex.Message });
            }
        }

        public IActionResult GetDebugInfo()
        {
            try
            {
                var allTanks = _context.Tank.ToList();
                var allProducts = _context.Master_Product.ToList();

                var tankCount = allTanks.Count;
                var productCount = allProducts.Count;

                var tankProductIds = allTanks
                    .Select(t => new
                    {
                        Tank_Name = t.Tank_Name,
                        Product_ID_Guid = t.Product_ID,
                        Product_ID_Str = t.Product_ID.ToString()
                    })
                    .Distinct()
                    .Take(5)
                    .ToList();

                var productSample = allProducts
                    .Select(p => new
                    {
                        Product_ID = p.Product_ID,
                        Product_ID_Str = p.Product_ID.ToString(),
                        Product_Code = p.Product_Code,
                        Product_Name = p.Product_Name
                    })
                    .Take(5)
                    .ToList();

                var matchAttempts = allTanks
                    .Take(5)
                    .Select(t =>
                    {
                        var matchByCode = allProducts.FirstOrDefault(p => p.Product_Code == t.Product_ID);

                        return new
                        {
                            Tank_Name = t.Tank_Name,
                            Tank_Product_ID = t.Product_ID,
                            Matches_By_CODE = matchByCode?.Product_Name ?? "NO MATCH"
                        };
                    })
                    .ToList();

                return Json(new
                {
                    message = "Check console/application logs for complete analysis",
                    tankCount = tankCount,
                    productCount = productCount,
                    tank_product_id_samples = tankProductIds,
                    product_samples = productSample,
                    match_attempts = matchAttempts,
                    note = "Tank.Product_ID now stores Product_Code (string). Check if 'Matches_By_CODE' finds matches."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDebugInfo()");
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public void InitialHome()
        {
            try
            {
                var totalTanks = _context.Tank.Count();
                _logger.LogInformation($"=== InitialHome() Debug START ===");
                _logger.LogInformation($"Tanks before filter: {totalTanks}");

                var allTanks = _context.Tank.ToList();

                var tanksWithEmptyProductId = allTanks.Count(t => string.IsNullOrEmpty(t.Product_ID));
                var tanksWithNullVolume = allTanks.Count(t => t.TankVolume == null);
                var tanksWithZeroVolume = allTanks.Count(t => t.TankVolume != null && t.TankVolume == 0);
                var tanksWithZeroHeight = allTanks.Count(t => t.Tank_Height == 0);

                _logger.LogInformation($"Tanks with empty Product_ID: {tanksWithEmptyProductId}");
                _logger.LogInformation($"Tanks with NULL TankVolume: {tanksWithNullVolume}");
                _logger.LogInformation($"Tanks with TankVolume = 0: {tanksWithZeroVolume}");
                _logger.LogInformation($"Tanks with Tank_Height = 0: {tanksWithZeroHeight}");

                _logger.LogInformation("=== Sample tanks BEFORE filter (first 3): ===");
                foreach (var t in allTanks.Take(3))
                {
                    _logger.LogInformation($"Tank: {t.Tank_Name}, Product_ID: '{t.Product_ID}', TankVolume: {t.TankVolume}, Tank_Height: {t.Tank_Height}");
                }

                var tanks = _context.Tank
                    .Where(t => !string.IsNullOrEmpty(t.Product_ID)
                        && t.Tank_Height > 0)
                    .ToList();

                _logger.LogInformation($"Tanks after filter (step 1): {tanks.Count}");

                // Show tanks that passed filter
                if (tanks.Count > 0)
                {
                    _logger.LogInformation("=== Tanks that PASSED filter: ===");
                    foreach (var t in tanks.Take(3))
                    {
                        _logger.LogInformation($"Tank: {t.Tank_Name}, Product_ID: '{t.Product_ID}', TankVolume: {t.TankVolume}, Tank_Height: {t.Tank_Height}");
                    }
                }
                else
                {
                    _logger.LogWarning("NO TANKS passed the filter! All tanks were filtered out.");
                }

                // STEP 4: Load products and check for matches
                var products = _context.Master_Product.ToList();
                _logger.LogInformation($"Total products loaded: {products.Count}");

                // Show sample products
                _logger.LogInformation("=== Sample products (first 3): ===");
                foreach (var p in products.Take(3))
                {
                    _logger.LogInformation($"Product_Code: '{p.Product_Code}', Product_Name: '{p.Product_Name}'");
                }

                // STEP 5: Check if Product_IDs from tanks match any Product_Code in products
                if (tanks.Count > 0)
                {
                    _logger.LogInformation("=== Checking Product_ID matches: ===");
                    foreach (var t in tanks.Take(3))
                    {
                        var matchingProduct = products.FirstOrDefault(p => p.Product_Code == t.Product_ID);
                        _logger.LogInformation($"Tank '{t.Tank_Name}' Product_ID='{t.Product_ID}' matches product: {(matchingProduct != null ? matchingProduct.Product_Name : "NO MATCH")}");
                    }
                }

                // STEP 6: Manual join menggunakan SelectMany + Where dengan Product_Code comparison
                // ✅ FIX: Custom sort order untuk dashboard (T-10, T-1, T-2, T-3, T-11, T-4, T-5, T-6, T-15, T-7, T-8, T-9)
                
                // ✅ CUSTOM SORT ORDER: Urutan yang diinginkan
                var customOrder = new List<string> 
                { 
                    "T-10", "T-1", "T-2", "T-3",   // Baris 1
                    "T-11", "T-4", "T-5", "T-6",   // Baris 2
                    "T-15", "T-7", "T-8", "T-9"    // Baris 3
                };
                
                var Tanks = tanks
                    .SelectMany(t => products.Where(p => p.Product_Code == t.Product_ID),
                        (t, p) => new
                        {
                            TankId = t.Tank_Name ?? "Unknown",
                            TempId = "temp" + (t.Tank_Name ?? "Unknown"),
                            ImageId = "img" + (t.Tank_Name ?? "Unknown"),
                            AlertId = "alert" + (t.Tank_Name ?? "Unknown"),
                            FlowrateId = "flowrate" + (t.Tank_Name ?? "Unknown"),
                            DensityId = "density" + (t.Tank_Name ?? "Unknown"),
                            Name = t.Tank_Name ?? "Unknown",
                            Tank_Name = t.Tank_Name ?? "Unknown",
                            ProductName = p?.Product_Name ?? "Unknown",
                            Product_Name = p?.Product_Name ?? "Unknown",
                            HexColor = p?.HexColor ?? "#000000"
                        })
                    // ✅ CUSTOM SORT: Urutan sesuai customOrder, tank yang tidak ada di list di akhir
                    .OrderBy(t => 
                    {
                        var index = customOrder.IndexOf(t.Tank_Name);
                        return index >= 0 ? index : int.MaxValue; // Tank tidak dalam list di akhir
                    })
                    .ThenBy(t => t.Tank_Name) // Secondary sort untuk tank yang tidak dalam list
                    .ToList();

                _logger.LogInformation($"Tanks after join with products: {Tanks.Count}");

                if (Tanks.Count > 0)
                {
                    _logger.LogInformation("=== Final ViewBag.Tanks (first 3): ===");
                    foreach (var t in Tanks.Take(3))
                    {
                        _logger.LogInformation($"Tank: {t.Tank_Name}, Product: {t.ProductName}, TankId: {t.TankId}");
                    }
                }
                else
                {
                    _logger.LogWarning("ViewBag.Tanks is EMPTY after join! No cards will display.");
                }

                ViewBag.Tanks = Tanks;
                _logger.LogInformation($"=== InitialHome() Debug END ===");
            }
            catch (System.Data.SqlTypes.SqlNullValueException ex)
            {
                _logger.LogError(ex, "Error loading tank data - NULL value encountered in database");
                ViewBag.Tanks = new List<object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading tank data");
                ViewBag.Tanks = new List<object>();
            }
        }

        // GET: GetOperationalStatus
        public IActionResult GetOperationalStatus(string tankName)
        {
            var tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == tankName);
            if (tank == null) return NotFound();

            // ✅ NEW: Load existing movement data
            var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == tankName);

            // ✅ Convert Status to OperationType string
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

            // ✅ NEW: Check if tank can disable alarm (KHUSUS T-10 dan T-11)
            bool canDisableAlarm = tankName == "T-10" || tankName == "T-11";

            var model = new TankSafetyOperation
            {
                TankName = tank.Tank_Name,
                // ✅ FIX: Explicit cast dari double? ke double dengan null-coalescing
                MaxLevel = (tank.LevelH ?? 0) > 0 ? (tank.LevelH ?? 0) : tank.Height_Safe_Capacity,
                MinLevel = tank.LevelL ?? 0,
                // ✅ NEW: Load saved values from Tank_Movement
                OperationType = operationType,
                TargetLevel = movement?.TargetLevel,
                StagnantThresholdMinutes = movement?.StagnantThresholdMinutes,
                DisableOperationAlarm = movement?.DisableOperationAlarm ?? false,
                Desc = ""
            };

            return PartialView("~/Views/TankLiveDatas/_OperationalStatus.cshtml", model);
        }

        // POST: SaveOperationalStatus
        [HttpPost]
        public async Task<IActionResult> SaveOperationalStatus(TankSafetyOperation model, int TargetLevel, int? StagnantThresholdMinutes)
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
                        AlarmTimeStamp = DateTime.Now.TimeOfDay
                    };
                    _context.Tank_Movement.Add(tankMovement);
                }

                if (model.OperationType == "RECEIVING")
                {
                    tankMovement.Status = 1;
                    tankMovement.TargetLevel = TargetLevel; // ✅ Simpan untuk RECEIVING
                    tankMovement.StagnantThresholdMinutes = StagnantThresholdMinutes;
                }
                else if (model.OperationType == "SALES")
                {
                    tankMovement.Status = 2;
                    // ✅ FIX: SALES hanya ubah status, tidak ubah TargetLevel
                }
                else if (model.OperationType == "STANDBY")
                {
                    tankMovement.Status = 0;
                    // ✅ FIX: STANDBY tidak clear data
                }
                else
                {
                    return Json(new { success = false, message = "Invalid Operation Type" });
                }

                tankMovement.TimeStamp = DateTime.Now;
                tankMovement.StagnantAlarm = false;
                tankMovement.LastFlowrateChangeTime = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Successfully updated status for {model.TankName} to {model.OperationType}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}