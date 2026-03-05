using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CSL.Web.Models;
using CSL.Web;
using TMS.Models;
using Microsoft.AspNetCore.Authorization;
using TMS.Web.Areas.Identity.Data;
using System.IO;
using TMS.Web.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using MathNet.Numerics.Distributions;
using NPOI.HSSF.Record;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TMS.Web.Controllers
{
    public class TankHistoricalController : Controller
    {
        private readonly TMSContext _context;
        public TankHistoricalController(TMSContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            populateTanks();
            return View();
        }


        //LOAD DATA MENGGUNAKAN AS NO TRACKING
        public IActionResult LoadData()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault(); 
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault(); 
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();   
                int pageSize = length != null ? Convert.ToInt32(length) : 0;   
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;


                var tankName = Request.Form["TankNameFilter"].FirstOrDefault();//Tank Name
                var Datefrom = DateTime.Parse(Request.Form["DateFrom"].FirstOrDefault());//Operation Type
                var DateTo = DateTime.Parse(Request.Form["DateTo"].FirstOrDefault()).AddDays(1).AddSeconds(-1);//Operation Type
                var TimeFilter = Request.Form["TimeFilter"].FirstOrDefault();

                // Handle "All Tank" filter variations
                bool isAllTanks = string.IsNullOrEmpty(tankName) ||
                                  tankName == "0" ||
                                  tankName.Contains("All Tank") ||
                                  tankName.Contains("----");

                var th = _context.Tank_Historical.Where(t => (isAllTanks || t.Tank_Number == tankName)
                                        && t.TimeStamp >= Datefrom
                                        && t.TimeStamp <= DateTo).OrderByDescending(e => e.Id).AsNoTracking().ToList();

                // Debug logging
                Console.WriteLine($"Tank Historical count before join: {th.Count}");
                Console.WriteLine($"Date range: {Datefrom} to {DateTo}");
                Console.WriteLine($"Tank filter: {tankName} (isAllTanks: {isAllTanks})");

                var tankhistorical = (from t in th
                                      join p in _context.Tank on t.Tank_Number equals p.Tank_Name into tankJoin
                                      from p in tankJoin.DefaultIfEmpty()
                                      join c in _context.Master_Product on (p != null ? p.Product_ID : "") equals c.Product_Code into productJoin
                                      from c in productJoin.DefaultIfEmpty()
                                      select new
                                      {
                                          Tank_Name = t.Tank_Number,  // Use Tank_Number value for display (same as Tank.Tank_Name)
                                          Product_Name = c != null ? c.Product_Name : "Unknown",
                                          dataTime = Convert.ToDateTime(t.TimeStamp),
                                          t.TimeStamp,
                                          LiquidLevel = t.Level ?? 0,
                                          WaterLevel = t.Level_Water ?? 0,
                                          LiquidTemperature = t.Temperature ?? 0,
                                          TestTemperature = t.Temperature ?? 0,
                                          LiquidDensity = t.Density ?? 0,
                                          Volume = t.Volume_Obs ?? 0,
                                          // ✅ UPDATED 2025-12-16: Get from historical table (not LiveData)
                                          PumpPamble = t.Pumpable ?? 0,
                                          Ullage = t.Ullage ?? 0,
                                          FlowRateKLperH = t.Flowrate ?? 0,
                                          // ✅ NEW FIELDS
                                          AlarmStatus = t.Alarm_Status,
                                          AlarmMessage = t.AlarmMessage,
                                          Status = "",
                                          DefaultKLperH = p != null && p.DefaultKLperH != null ? Convert.ToDouble(p.DefaultKLperH) : 0.0
                                      }).ToList();

                Console.WriteLine($"Tank Historical count after join: {tankhistorical.Count}");


                if (TimeFilter == "10min")
                {
                    tankhistorical = tankhistorical
                        .GroupBy(t => new
                        {
                            t.Tank_Name,
                            t.dataTime.Date,
                            t.dataTime.Hour,
                            MinuteGroup = (t.dataTime.Minute / 10) * 10
                        })
                        .Select(g => g.OrderBy(t => t.dataTime).First())
                        .Select(t => new
                        {
                            t.Tank_Name,
                            t.Product_Name,
                            dataTime = new DateTime(t.dataTime.Year, t.dataTime.Month, t.dataTime.Day, t.dataTime.Hour, (t.dataTime.Minute / 10) * 10, 0), 
                            t.TimeStamp,
                            t.LiquidLevel,
                            t.WaterLevel,
                            t.LiquidTemperature,
                            t.TestTemperature,
                            t.LiquidDensity,
                            t.Volume,
                            t.PumpPamble,
                            t.Ullage,
                            FlowRateKLperH = t.FlowRateKLperH,
                            t.AlarmStatus,
                            t.AlarmMessage,
                            t.Status,
                            t.DefaultKLperH
                        })
                        .ToList();
                }

                else if (TimeFilter == "30min")
                {
                    tankhistorical = tankhistorical
                        .GroupBy(t => new
                        {
                            t.Tank_Name,
                            t.dataTime.Date,
                            t.dataTime.Hour,
                            MinuteGroup = (t.dataTime.Minute / 30) * 30
                        })
                        .Select(g => g.OrderBy(t => t.dataTime).First())
                        .Select(t => new
                        {
                            t.Tank_Name,
                            t.Product_Name,
                            dataTime = new DateTime(t.dataTime.Year, t.dataTime.Month, t.dataTime.Day, t.dataTime.Hour, (t.dataTime.Minute / 30) * 30, 0), 
                            t.TimeStamp,
                            t.LiquidLevel,
                            t.WaterLevel,
                            t.LiquidTemperature,
                            t.TestTemperature,
                            t.LiquidDensity,
                            t.Volume,
                            t.PumpPamble,
                            t.Ullage,
                            FlowRateKLperH = t.FlowRateKLperH,
                            t.AlarmStatus,
                            t.AlarmMessage,
                            t.Status,
                            t.DefaultKLperH
                        })
                        .ToList();
                }
                else if (TimeFilter == "hour")
                {
                    tankhistorical = tankhistorical
                        .GroupBy(t => new
                        {
                            t.Tank_Name,
                            t.dataTime.Date,
                            t.dataTime.Hour
                        })
                        .Select(g => g.OrderBy(t => t.dataTime.Minute).ThenBy(t => t.dataTime.Second).First())
                        .ToList();
                }
                else if (TimeFilter == "min")
                {
                    tankhistorical = tankhistorical
                        .GroupBy(t => new
                        {
                            t.Tank_Name,
                            t.dataTime.Date,
                            t.dataTime.Hour,
                            t.dataTime.Minute 
                        })
                        .Select(g => g.OrderBy(t => t.dataTime.Second).First()) 
                        .ToList();
                }


                var tankhistoricalWithFlowRate = tankhistorical
                    .GroupBy(t => t.Tank_Name)
                    .SelectMany(g =>
                    {
                        var list = g.ToList();
                        var withFlowRate = list
                            .Select((current, index) => new
                            {
                                current.Tank_Name,
                                current.Product_Name,
                                current.dataTime,
                                current.TimeStamp,
                                current.LiquidLevel,
                                current.WaterLevel,
                                current.LiquidTemperature,
                                current.TestTemperature,
                                current.LiquidDensity,
                                current.Volume,
                                current.PumpPamble,
                                current.Ullage,
                                FlowRateKLperH = index == 0 ? 0.0 : GetFlowRateKLperH(TimeFilter, current.LiquidLevel, list[index - 1].LiquidLevel, current.Volume, list[index - 1].Volume, current.FlowRateKLperH, current.DefaultKLperH),
                                current.Status
                            }).ToList();

                        return withFlowRate;
                    });
                tankhistoricalWithFlowRate = tankhistoricalWithFlowRate.OrderByDescending(t => t.TimeStamp);

                //total number of rows counts
                recordsTotal = tankhistoricalWithFlowRate.Count();
                //Paging
                var data = tankhistoricalWithFlowRate.Skip(skip).Take(pageSize).ToList();

                // Debug logging
                Console.WriteLine($"Final data count: {data.Count}");
                if (data.Count > 0)
                {
                    var firstItem = data.First();
                    Console.WriteLine($"First item properties - Tank_Name: {firstItem.Tank_Name}, Product_Name: {firstItem.Product_Name}");
                }

                //Returning Json Data
                var result = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data };
                return Json(result, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null // Preserve property name casing
                });
            }
            catch (Exception)
            {
                throw;
            }
        }


        //LOAD DATA MENGGUNAKAN PAGINATION
        public IActionResult LoadDataV2()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var pageSize = length != null ? Convert.ToInt32(length) : 10;
                var skip = start != null ? Convert.ToInt32(start) : 0;

                var tankName = Request.Form["TankNameFilter"].FirstOrDefault();
                var DateFrom = DateTime.Parse(Request.Form["DateFrom"].FirstOrDefault());
                var DateTo = DateTime.Parse(Request.Form["DateTo"].FirstOrDefault());
                var TimeFilter = Request.Form["TimeFilter"].FirstOrDefault();

                // Handle "All Tank" filter variations
                bool isAllTanks = string.IsNullOrEmpty(tankName) ||
                                  tankName == "0" ||
                                  tankName.Contains("All Tank") ||
                                  tankName.Contains("----");

                var query = _context.Tank_Historical
                    .Where(t => (isAllTanks || t.Tank_Number == tankName) &&
                                t.TimeStamp >= DateFrom &&
                                t.TimeStamp <= DateTo)
                    .AsNoTracking(); 

                if (TimeFilter == "10min")
                {
                    query = query.Where(t => t.TimeStamp.Value.Minute % 10 == 0 && t.TimeStamp.Value.Second == 0);
                }
                else if (TimeFilter == "30min")
                {
                    query = query.Where(t => t.TimeStamp.Value.Minute % 30 == 0 && t.TimeStamp.Value.Second == 0);
                }
                else if (TimeFilter == "hour")
                {
                    query = query.Where(t => t.TimeStamp.Value.Minute == 0 && t.TimeStamp.Value.Second == 0);
                }
                else if (TimeFilter == "min")
                {
                    query = query.Where(t => t.TimeStamp.Value.Second == 0);
                }

                var recordsTotal = query.Count();
                
                var tankhistorical = (from t in query
                                      join p in _context.Tank on t.Tank_Number equals p.Tank_Name
                                      join c in _context.Master_Product on p.Product_ID equals c.Product_Code
                                      select new
                                      {
                                          t.Tank_Number,
                                          p.Tank_Name,
                                          c.Product_Name,
                                          TimeStamp = t.TimeStamp.Value,
                                          LiquidLevel = t.Level ?? 0,
                                          WaterLevel = t.Level_Water ?? 0,
                                          LiquidTemperature = t.Temperature ?? 0,
                                          TestTemperature = t.Temperature ?? 0,
                                          LiquidDensity = t.Density ?? 0,
                                          Volume = t.Volume_Obs ?? 0,
                                          // ✅ UPDATED 2025-12-16: Get from historical table (not LiveData)
                                          PumpPamble = t.Pumpable ?? 0,
                                          Ullage = t.Ullage ?? 0,
                                          FlowRateKLperH = t.Flowrate ?? 0,
                                          // ✅ NEW FIELDS
                                          AlarmStatus = t.Alarm_Status,
                                          AlarmMessage = t.AlarmMessage,
                                          Status = "",
                                          DefaultKLperH = p.DefaultKLperH ?? 0.0
                                      })
                                      .OrderByDescending(t => t.TimeStamp)
                                      .Skip(skip)
                                      .Take(pageSize)
                                      .ToList();


                var tankhistoricalWithFlowRate = tankhistorical
                    .Select((current, index) => new
                    {
                        current.Tank_Number,
                        current.Tank_Name,
                        current.Product_Name,
                        current.TimeStamp,
                        current.LiquidLevel,
                        current.WaterLevel,
                        current.LiquidTemperature,
                        current.TestTemperature,
                        current.LiquidDensity,
                        current.Volume,
                        current.PumpPamble,
                        current.Ullage,
                        FlowRateKLperH = index == 0 ? 0.0 :
                            GetFlowRateKLperH(TimeFilter,
                                              current.LiquidLevel, tankhistorical[index - 1].LiquidLevel,
                                              current.Volume, tankhistorical[index - 1].Volume,
                                              current.FlowRateKLperH, current.DefaultKLperH),
                        current.Status
                    })
                    .ToList();

                // Return data dalam format yang sesuai untuk DataTables
                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsTotal, // Harus total data SEBELUM pagination
                    recordsTotal = recordsTotal,    // Harus total data SEBELUM pagination
                    data = tankhistoricalWithFlowRate
                }, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null // Preserve property name casing
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        private double GetFlowRateKLperH(string TimeFilter, double level, double LastLevel, double Volume, double LastVome, double KLperHour, double DefaultKLperHour)
        {
            level = Math.Round(level,0);
            LastLevel = Math.Round(LastLevel,0);
            Volume = Math.Round(Volume,0);
            LastVome = Math.Round(LastVome, 0);
            KLperHour = Math.Round(KLperHour, 0);
            if((level - LastLevel) == 0)
            {
                return 0.0;
            }
            //if((level - LastLevel) == 1)
            //{
            //    return DefaultKLperHour;
            //}
            //else if((level - LastLevel) == -1)
            //{
            //    return DefaultKLperHour * -1;
            //}
            var difVolume = Math.Round(LastVome, 0) - Math.Round(Volume, 0);
            if (TimeFilter == "10min")
            {
                return (difVolume * 6) / 1000;
            }
            else if (TimeFilter == "30min")
            {
                return (difVolume *2) / 1000;
            }
            else if (TimeFilter == "hour")
            {
                return difVolume / 1000;
            }
            else if (TimeFilter == "min")
            {
                return (difVolume * 60) / 1000;
            }
            else
            {
                //return (difVolume * 3600) / 1000;
                return KLperHour;
            }
        }
        DateTime? GetIntervalStart(DateTime? recordedTimestamp, string timeInterval)
        {
            if (recordedTimestamp.HasValue)
            {
                switch (timeInterval)
                {
                    case "10min":
                        if(recordedTimestamp.Value.Minute == 0 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(0).AddSeconds(0);
                        }
                        else if (recordedTimestamp.Value.Minute == 10 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(10).AddSeconds(0);
                        }
                        else if (recordedTimestamp.Value.Minute == 20 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(20).AddSeconds(0);
                        }
                        else if (recordedTimestamp.Value.Minute == 30 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(30).AddSeconds(0);
                        }
                        else if (recordedTimestamp.Value.Minute == 40 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(40).AddSeconds(0);
                        }
                        else if (recordedTimestamp.Value.Minute == 50 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(50).AddSeconds(0);
                        }
                        break;
                    case "30min":
                        if ((recordedTimestamp.Value.Minute == 0 && recordedTimestamp.Value.Second ==0) || (recordedTimestamp.Value.Minute == 30 && recordedTimestamp.Value.Second == 0))
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(recordedTimestamp.Value.Minute).AddSeconds(0);
                        }
                        break;
                    case "min":
                        if (recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(recordedTimestamp.Value.Minute).AddSeconds(0);
                        }
                        break;
                    case "hour":
                        if (recordedTimestamp.Value.Minute == 0 && recordedTimestamp.Value.Second == 0)
                        {
                            return recordedTimestamp.Value.Date.AddYears(recordedTimestamp.Value.Year).AddMonths(recordedTimestamp.Value.Month).AddDays(recordedTimestamp.Value.Day).AddHours(recordedTimestamp.Value.Hour).AddMinutes(0).AddSeconds(0);

                        }
                        break;
                    case "sec":
                        return recordedTimestamp.Value;
                    // Add additional cases as needed
                    default:
                        throw new ArgumentException("Invalid timeInterval");
                }
            }
            return null;
        }

        private void populateTanks(object SelectList = null)
        {
            List<Tank> tanks = new List<Tank>();
            tanks = (from t in _context.Tank select t).ToList();
            var tankip = new Tank()
            {
                Tank_Name = "---- All Tank ----"
            };

            tanks.Insert(0, tankip);
            tanks = tanks.OrderBy(t => t.Tank_Name).ToList();
            ViewBag.TankId = tanks;
        }

        /// <summary>
        /// Get value from Tank_LiveData based on closest timestamp to historical timestamp
        /// Uses LastTimeStamp from Tank_LiveData to find the closest match
        /// </summary>
        private double GetClosestLiveDataValue(string tankNumber, DateTime? historicalTimestamp, string fieldName)
        {
            if (!historicalTimestamp.HasValue || string.IsNullOrEmpty(tankNumber))
                return 0.0;

            try
            {
                // Get all Tank_LiveData records for this tank that have LastTimeStamp
                var liveDataRecords = _context.Tank_Live_Data
                    .Where(t => t.Tank_Number == tankNumber && t.LastTimeStamp != null)
                    .AsNoTracking()
                    .ToList();

                if (!liveDataRecords.Any())
                    return 0.0;

                // Find the record with LastTimeStamp closest to the historical timestamp
                var closestRecord = liveDataRecords
                    .OrderBy(t => Math.Abs((t.LastTimeStamp.Value - historicalTimestamp.Value).TotalSeconds))
                    .FirstOrDefault();

                if (closestRecord == null)
                    return 0.0;

                // Return the requested field value
                switch (fieldName.ToLower())
                {
                    case "pumpable":
                        return closestRecord.Pumpable ?? 0.0;
                    case "ullage":
                        return closestRecord.Ullage;  // Ullage is double (non-nullable)
                    case "flowrate":
                        return closestRecord.Flowrate ?? 0.0;
                    default:
                        return 0.0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetClosestLiveDataValue: {ex.Message}");
                return 0.0;
            }
        }


        ////Histo Export To Excel 
        [HttpPost]
        public IActionResult ExportExcel(HistoricalFilter filter)
        {
            try
            {

                // Open the template file
                FileStream templateStream = new FileStream("Doc/TemplateTankHistorical.xls", FileMode.Open, FileAccess.Read);

                // Create a new workbook object based on the template file
                HSSFWorkbook workbook = new HSSFWorkbook(templateStream);

                // Get the sheet you want to write data to
                ISheet sheet = workbook.GetSheet("Historical");

                // Set Date Export
                IRow rowDate = sheet.CreateRow(5);
                rowDate.CreateCell(1).SetCellValue("Date Export : ");
                rowDate.CreateCell(2).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

                // Get the data model from your database or other source
                // Handle "All Tank" filter variations
                bool isAllTanks = string.IsNullOrEmpty(filter.TankNameFilter) ||
                                  filter.TankNameFilter == "0" ||
                                  filter.TankNameFilter.Contains("All Tank") ||
                                  filter.TankNameFilter.Contains("----");

                // Adjust DateTo to include the entire day
                DateTime dateToAdjusted = filter.DateTo.AddDays(1).AddSeconds(-1);

                Console.WriteLine($"Export Excel - Tank: {filter.TankNameFilter}, isAllTanks: {isAllTanks}");
                Console.WriteLine($"Export Excel - DateFrom: {filter.DateFrom}, DateTo: {dateToAdjusted}");
                Console.WriteLine($"Export Excel - TimeFilter: {filter.TimeFilter}");

                var historicalData = _context.Tank_Historical
                        .Where(d => (isAllTanks || d.Tank_Number == filter.TankNameFilter) &&
                                    d.TimeStamp >= filter.DateFrom &&
                                    d.TimeStamp <= dateToAdjusted)
                        .OrderByDescending(e => e.TimeStamp)
                        .ToList();  // Materialize query to close DataReader

                Console.WriteLine($"Export Excel - Historical data count: {historicalData.Count}");

                // Apply time filter if not "sec" (second)
                List<TankHistorical> groupedData;
                if (filter.TimeFilter != "sec")
                {
                    groupedData = historicalData
                        .Where(d => GetIntervalStart(d.TimeStamp, filter.TimeFilter) != null)
                        .GroupBy(d => new
                        {
                            IntervalStart = GetIntervalStart(d.TimeStamp, filter.TimeFilter),
                            Tank_Number = d.Tank_Number
                        })
                        .Select(g => g.OrderByDescending(e => e.Id).FirstOrDefault())
                        .ToList();  // Materialize to close any potential readers
                }
                else
                {
                    groupedData = historicalData;
                }

                Console.WriteLine($"Export Excel - Grouped data count: {groupedData.Count}");

                //var orderedData = groupedData.OrderByDescending(e => e.Id);

                var tankhistorical = from t in groupedData
                                     join p in _context.Tank on t.Tank_Number equals p.Tank_Name into tankJoin
                                     from p in tankJoin.DefaultIfEmpty()
                                     join c in _context.Master_Product on (p != null ? p.Product_ID : "") equals c.Product_Code into productJoin
                                     from c in productJoin.DefaultIfEmpty()
                                     select new
                                     {
                                         t.Tank_Number,
                                         Tank_Name = p != null ? p.Tank_Name : t.Tank_Number,
                                         Product_Name = c != null ? c.Product_Name : "Unknown",
                                         dataTime = Convert.ToDateTime(t.TimeStamp),
                                         t.TimeStamp,
                                         LiquidLevel = t.Level ?? 0,
                                         WaterLevel = t.Level_Water ?? 0,
                                         LiquidTemperature = t.Temperature ?? 0,
                                         TestTemperature = t.Temperature ?? 0,
                                         LiquidDensity = t.Density ?? 0,
                                         Volume = t.Volume_Obs ?? 0,
                                         // ✅ UPDATED 2025-12-16: Get from historical table (not LiveData)
                                         PumpPamble = t.Pumpable ?? 0,
                                         Ullage = t.Ullage ?? 0,
                                         FlowRateKLperH = t.Flowrate ?? 0,
                                         // ✅ NEW FIELDS
                                         AlarmStatus = t.Alarm_Status,
                                         AlarmMessage = t.AlarmMessage,
                                         Status = ""
                                     };
                // Time filtering already handled in groupedData above
                tankhistorical = tankhistorical.OrderByDescending(t => t.TimeStamp);

                Console.WriteLine($"Export Excel - Final tank historical count: {tankhistorical.Count()}");

                // Write the data model to the cells in the sheet
                int rowIndex = 9;
                int number = 1;
                // Start at row 1 (0-based index) to skip the header row
                foreach (var t in tankhistorical)
                {

                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue(number);
                    row.CreateCell(1).SetCellValue(t.Tank_Name);
                    row.CreateCell(2).SetCellValue(t.Product_Name);
                    row.CreateCell(3).SetCellValue(Convert.ToDateTime(t.TimeStamp).ToString("yyyy MMMM dd HH:mm:ss"));
                    row.CreateCell(4).SetCellValue(Math.Round((double)t.LiquidLevel,0));
                    row.CreateCell(5).SetCellValue(Math.Round((double)t.WaterLevel,0));
                    row.CreateCell(6).SetCellValue(Math.Round((double)t.LiquidTemperature,2));
                    row.CreateCell(7).SetCellValue(Math.Round((double)t.TestTemperature, 2));
                    row.CreateCell(8).SetCellValue(Math.Round((double)t.LiquidDensity,3));
                    row.CreateCell(9).SetCellValue(Math.Round((double)t.Volume,0));
                    row.CreateCell(10).SetCellValue(Math.Round((double)t.PumpPamble,0));
                    row.CreateCell(11).SetCellValue(Math.Round((double)t.Ullage,0));
                    row.CreateCell(12).SetCellValue(Math.Round((double)t.FlowRateKLperH,0));
                    rowIndex++;
                    number++;
                }

                // Save the workbook to a new file
                MemoryStream stream = new MemoryStream();
                workbook.Write(stream);
                stream.Position = 0;

                // Create filename with date range
                string dateFromStr = filter.DateFrom.ToString("dd MMM yyyy");
                string dateToStr = filter.DateTo.ToString("dd MMM yyyy");
                string tankName = isAllTanks ? "All Tanks" : filter.TankNameFilter;
                string fileName = $"Tank Historical {tankName} {dateFromStr} to {dateToStr}.xls";

                FileStreamResult file = new FileStreamResult(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                file.FileDownloadName = fileName;

                // Close the streams
                templateStream.Close();

                return file;
            }
            catch
            {
                throw;
            }

        }
    }
}
