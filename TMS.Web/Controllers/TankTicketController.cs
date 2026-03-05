using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TMS.Web.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using CSL.Web;
using TMS.Web;
using CSL.Web.Models;
using TMS.Web.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using TMS.Models;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NPOI.OpenXmlFormats.Spreadsheet;
using System.Globalization;
using DocumentFormat.OpenXml.Office2010.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.IO;
using System.ServiceModel.Channels;
using NPOI.SS.Formula.Functions;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class TankTicketController : Controller
    {
        public class TankTicketData
        {
            public int Tank_Id { get; set; }
            public string Shipment_Id { get; set; }
            public string Operation_Type { get; set; }
            public string Do_Number { get; set; }
            public string Ticket_Number { get; set; }
            public int Operation_Status { get; set; }
            public string Timestamp { get; set; }
            public string? Measurement_Method { get; set; }
            public string LiquidLevel { get; set; }
            public string WaterLevel { get; set; }
            public string LiquidTemperature { get; set; }
            public string LiquidDensity { get; set; }
            public string CheckStatusOpen { get; set; }
            public string CheckStatusClose { get; set; }
            public string IsPosting { get; set; }
        }

        public class TankTicketFormData
        {
            public TankTicketData Form1 { get; set; }
            public TankTicketData OpenFormData { get; set; }
            public TankTicketData CloseFormData { get; set; }
        }

        private readonly TMSContext _context;
        public IConfiguration _configuration { get; }

        public TankTicketController(TMSContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        // GET: Tanks
        [Authorize]
        public IActionResult Index()
        {
            // ❌ DISABLED: Alarm logic - temporarily disabled
            // bool triggerAlarm = true;
            // if (triggerAlarm)
            // {
            //     ViewBag.PlayAlarm = true;
            // }

            // Read refresh interval from appsettings.json (default 10 seconds)
            var refreshInterval = _configuration.GetValue<int>("UIRefreshSettings:TankTicketRefreshSeconds", 10);
            ViewBag.RefreshIntervalSeconds = refreshInterval;

            // ❌ DISABLED: Alarm polling - temporarily disabled
            // var alarmPollingMs = _configuration.GetValue<int>("PollingSettings:TicketNoFlowrateAlarmPollingMs", 3000);
            // ViewBag.TicketNoFlowrateAlarmPollingMs = alarmPollingMs;

            populateTanks();
            populateTankTicketType();
            return View();
        }

        // ========== TICKET NO-FLOWRATE ALARM ENDPOINTS (DATABASE-BACKED) ==========

        /// <summary>
        /// ✅ Endpoint untuk detect no-flowrate alarm pada Tank Ticket OPEN (ROT/Receiving)
        /// Database-backed alarm state (Alarm_Status, Alarm_Ack_Time)
        /// Auto-clear saat ada flowrate, auto-reset setelah 5 menit dari ACK
        /// </summary>
        [HttpGet]
        public IActionResult TriggerTicketNoFlowrateAlarm()
        {
            try
            {
                // Get threshold dari config (dalam menit)
                int thresholdMinutes = _configuration.GetValue<int>(
                    "PollingSettings:TicketNoFlowrateThresholdMinutes", 5);
                double flowrateThreshold = 0.01; // KL/h - consider as "no flowrate"

                // ✅ Get all OPEN tickets untuk receiving operations (ROT = StatusReservasi 4)
                // Filter TODAY ONLY - avoid reading old tickets
                DateTime today = DateTime.Today; // 00:00:00 hari ini
                var openTickets = _context.Tank_Ticket
                    .Where(t => t.Operation_Status == 1 &&
                               t.StatusReservasi == 4 &&
                               t.Timestamp.HasValue &&
                               t.Timestamp.Value >= today)  // OPEN + ROT + TODAY only
                    .ToList();

                Console.WriteLine($"[TicketAlarm] Found {openTickets.Count} OPEN ROT tickets (TODAY: {today:yyyy-MM-dd})");

                var alarms = new List<object>();
                bool hasChanges = false;

                foreach (var ticket in openTickets)
                {
                    // Skip if ticket timestamp is null
                    if (!ticket.Timestamp.HasValue) continue;

                    var operationType = ticket.Operation_Type;

                    // Get tank info
                    var tank = _context.Tank
                        .FirstOrDefault(t => t.Tank_Name == ticket.Tank_Number);
                    if (tank == null) continue;

                    // Get live data untuk check flowrate
                    var liveData = _context.Tank_Live_Data
                        .FirstOrDefault(ld => ld.Tank_Number == ticket.Tank_Number);
                    if (liveData == null) continue;

                    // Check flowrate
                    var currentFlowrate = Math.Abs(liveData.Flowrate ?? 0);

                    // ❌ DISABLED: Alarm Status logic (AlarmStatus & AlarmAckTime) - temporarily disabled
                    /*
                    if (currentFlowrate >= flowrateThreshold)
                    {
                        // ✅ ADA FLOWRATE - AUTO CLEAR
                        if (ticket.AlarmStatus != 0)
                        {
                            ticket.AlarmStatus = 0;
                            ticket.AlarmAckTime = DateTime.Now;
                            hasChanges = true;
                            Console.WriteLine($"[TicketAlarm] AUTO-CLEAR: Ticket {ticket.Id} ({tank.Tank_Name}) - Flowrate: {currentFlowrate:F2} KL/h");
                        }
                        continue; // No alarm
                    }

                    // ✅ TIDAK ADA FLOWRATE
                    // Check alarm status
                    if (ticket.AlarmStatus == 0)
                    {
                        // Sudah di-ACK, check grace period (5 menit)
                        if (ticket.AlarmAckTime.HasValue)
                        {
                            var elapsedSinceAck = DateTime.Now - ticket.AlarmAckTime.Value;
                            if (elapsedSinceAck.TotalMinutes < thresholdMinutes)
                            {
                                // Masih dalam grace period (< 5 menit), skip alarm
                                continue;
                            }
                            else
                            {
                                // Grace period habis (>= 5 menit), auto-reset alarm
                                ticket.AlarmStatus = 1;
                                hasChanges = true;
                                Console.WriteLine($"[TicketAlarm] AUTO-RESET: Ticket {ticket.Id} ({tank.Tank_Name}) - Grace period expired ({elapsedSinceAck.TotalMinutes:F1} min)");
                            }
                        }
                        else
                        {
                            // AlarmStatus = 0 tapi tidak ada AlarmAckTime (anomali)
                            // Reset ke AlarmStatus = 1
                            ticket.AlarmStatus = 1;
                            hasChanges = true;
                            Console.WriteLine($"[TicketAlarm] AUTO-RESET: Ticket {ticket.Id} ({tank.Tank_Name}) - Missing AlarmAckTime");
                        }
                    }
                    */

                    // ✅ Check for no flowrate alarm (simplified - without alarm status)
                    var elapsedSinceTicket = DateTime.Now - ticket.Timestamp.Value;
                    var elapsedMinutes = Math.Round(elapsedSinceTicket.TotalMinutes, 0);

                    alarms.Add(new
                    {
                        TicketId = ticket.Id,
                        TankName = tank.Tank_Name,
                        OperationType = operationType,
                        Type = "No Flowrate",
                        Message = $"Tidak ada flowrate pada tangki penerimaan '{tank.Tank_Name}' selama {elapsedMinutes} menit",
                        AlarmType = "no_flowrate",
                        ElapsedMinutes = elapsedMinutes,
                        ShipmentId = ticket.Shipment_Id,
                        DoNumber = ticket.Do_Number,
                        TicketTimestamp = ticket.Timestamp,
                        // AlarmStatus = ticket.AlarmStatus,  // ❌ DISABLED
                        // AlarmAckTime = ticket.AlarmAckTime,  // ❌ DISABLED
                        CurrentFlowrate = currentFlowrate
                    });
                }

                // Save changes to database if any
                if (hasChanges)
                {
                    _context.SaveChanges();
                    Console.WriteLine($"[TicketAlarm] Saved alarm state changes to database");
                }

                return Json(new { isValid = alarms.Count > 0, data = alarms });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TicketAlarm] ERROR: {ex.Message}");
                return Json(new { isValid = false, error = ex.Message });
            }
        }

        // ❌ DISABLED: ACK single ticket alarm - temporarily disabled
        /*
        /// <summary>
        /// ✅ ACK single ticket alarm
        /// Sets Alarm_Status = 0 and Alarm_Ack_Time in database
        /// </summary>
        [HttpPost]
        public IActionResult AckTicketNoFlowrateAlarm([FromBody] long ticketId)
        {
            try
            {
                var ticket = _context.Tank_Ticket.Find(ticketId);
                if (ticket == null)
                {
                    return Json(new { isValid = false, error = "Ticket not found" });
                }

                // Set Alarm_Status = 0 (ACK) dan Alarm_Ack_Time
                ticket.AlarmStatus = 0;
                ticket.AlarmAckTime = DateTime.Now;
                _context.SaveChanges();

                Console.WriteLine($"[TicketAlarm] ACK: Ticket {ticketId} - Alarm acknowledged, grace period started");

                return Json(new { isValid = true, message = "Alarm acknowledged" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TicketAlarm] ACK ERROR: {ex.Message}");
                return Json(new { isValid = false, error = ex.Message });
            }
        }
        */

        // ❌ DISABLED: ACK all ticket alarms - temporarily disabled
        /*
        /// <summary>
        /// ✅ ACK all ticket alarms at once
        /// Sets Alarm_Status = 0 untuk semua OPEN ROT tickets
        /// </summary>
        [HttpPost]
        public IActionResult AckAllTicketNoFlowrateAlarms()
        {
            try
            {
                // ✅ Get all OPEN tickets untuk receiving operations (StatusReservasi 4 = ROT)
                // ✅ Filter TODAY ONLY
                DateTime today = DateTime.Today; // 00:00:00 hari ini
                var openTickets = _context.Tank_Ticket
                    .Where(t => t.Operation_Status == 1 &&
                               t.StatusReservasi == 4 &&
                               t.Timestamp.HasValue &&
                               t.Timestamp.Value >= today)
                    .ToList();

                int ackCount = 0;
                DateTime now = DateTime.Now;
                foreach (var ticket in openTickets)
                {
                    // Set Alarm_Status = 0 (ACK) dan Alarm_Ack_Time untuk semua
                    ticket.AlarmStatus = 0;
                    ticket.AlarmAckTime = now;
                    ackCount++;
                }

                _context.SaveChanges();

                Console.WriteLine($"[TicketAlarm] ACK ALL: {ackCount} tickets acknowledged");

                return Json(new { isValid = true, message = $"{ackCount} alarms acknowledged" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TicketAlarm] ACK ALL ERROR: {ex.Message}");
                return Json(new { isValid = false, error = ex.Message });
            }
        }
        */
        public IActionResult LoadData()
        {
            try
            {
                Console.WriteLine("=== LoadData Called ===");

                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Get filter values
                var tankNameFilter = Request.Form["TankNameFilter"].FirstOrDefault();
                var operationType = Request.Form["OperationType"].FirstOrDefault();
                var operationStatusStr = Request.Form["OperationStatus"].FirstOrDefault();
                var ticketNumber = Request.Form["TicketNumber"].FirstOrDefault();
                var minDateTimeStr = Request.Form["minDateTime"].FirstOrDefault();
                var maxDateTimeStr = Request.Form["maxDateTime"].FirstOrDefault();

                // ✅ OPTIMASI: Use AsNoTracking for read-only query
                var ticketsQuery = _context.Tank_Ticket.AsNoTracking().AsQueryable();

                // Apply Tank Name filter
                if (!string.IsNullOrEmpty(tankNameFilter) &&
                    tankNameFilter != "0" &&
                    tankNameFilter != "---- All Tank ----")
                {
                    ticketsQuery = ticketsQuery.Where(t => t.Tank_Number == tankNameFilter);
                }

                // Apply Operation Status filter
                if (!string.IsNullOrEmpty(operationStatusStr) &&
                    int.TryParse(operationStatusStr, out int operationStatus) &&
                    operationStatus > 0)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.Operation_Status == operationStatus);
                }

                // Apply Ticket Number filter
                if (!string.IsNullOrEmpty(ticketNumber))
                {
                    ticketsQuery = ticketsQuery.Where(t => t.Ticket_Number.Contains(ticketNumber));
                }

                // ✅ Apply Date filters - DEFAULT TO TODAY if empty
                DateTime minDateTime = DateTime.Today;
                DateTime maxDateTime = DateTime.Today.AddDays(1).AddTicks(-1);

                if (!string.IsNullOrEmpty(minDateTimeStr))
                {
                    var formats = new[] {
                        "dd MMMM yyyy", "d MMMM yyyy", "DD MMMM YYYY",
                        "dd MMM yyyy", "d MMM yyyy", "dd/MM/yyyy", "yyyy-MM-dd"
                    };

                    if (DateTime.TryParseExact(minDateTimeStr, formats,
                        System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                        System.Globalization.DateTimeStyles.None,
                        out var parsedMin))
                    {
                        minDateTime = parsedMin.Date;
                    }
                }

                ticketsQuery = ticketsQuery.Where(t => t.Timestamp >= minDateTime);

                if (!string.IsNullOrEmpty(maxDateTimeStr))
                {
                    var formats = new[] {
                        "dd MMMM yyyy", "d MMMM yyyy", "DD MMMM YYYY",
                        "dd MMM yyyy", "d MMM yyyy", "dd/MM/yyyy", "yyyy-MM-dd"
                    };

                    if (DateTime.TryParseExact(maxDateTimeStr, formats,
                        System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                        System.Globalization.DateTimeStyles.None,
                        out var parsedMax))
                    {
                        maxDateTime = parsedMax.Date.AddDays(1).AddTicks(-1);
                    }
                }

                ticketsQuery = ticketsQuery.Where(t => t.Timestamp <= maxDateTime);

                // Apply Operation Type filter
                if (!string.IsNullOrEmpty(operationType) &&
                    operationType != "0" &&
                    operationType != "--All Operation Type--")
                {
                    int? statusReservasi = operationType.ToUpper() switch
                    {
                        "PSC" => 1, "RD" => 2, "TT" => 3, "ROT" => 4,
                        "ILS" => 5, "PI" => 6, "CHK" => 7, "UDG" => 8, "BLD" => 9,
                        _ => null
                    };

                    if (statusReservasi.HasValue)
                    {
                        ticketsQuery = ticketsQuery.Where(t => t.StatusReservasi == statusReservasi.Value);
                    }
                }

                // Search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    ticketsQuery = ticketsQuery.Where(t =>
                        (t.Tank_Number != null && t.Tank_Number.Contains(searchValue)) ||
                        (t.Ticket_Number != null && t.Ticket_Number.Contains(searchValue)));
                }

                // ✅ OPTIMASI: Skip COUNT query - estimate recordsTotal
                // COUNT query is very slow on large tables
                int recordsTotal = -1; // Will be updated below
                
                // Sorting - Always sort by Timestamp DESC for performance
                ticketsQuery = ticketsQuery.OrderByDescending(t => t.Timestamp);

                // ✅ Get data with limit
                var tickets = ticketsQuery.Skip(skip).Take(pageSize).ToList();

                // ✅ OPTIMASI: Estimate total count (faster than COUNT(*))
                // If we got full page, there's probably more data
                if (tickets.Count == pageSize)
                {
                    // Estimate: there's more data (use -1 or large number)
                    recordsTotal = skip + pageSize + 1000; // Estimate
                }
                else
                {
                    recordsTotal = skip + tickets.Count;
                }

                Console.WriteLine($"Retrieved {tickets.Count} tickets");

                // Map to DTO
                var data = tickets.Select(t => new
                {
                    Id = t.Id,
                    Tank_Number = t.Tank_Number ?? "-",
                    Ticket_Number = t.Ticket_Number ?? "-",
                    Timestamp = t.Timestamp,
                    Operation_Type = GetOperationTypeText(t.StatusReservasi),
                    Operation_Status = t.Operation_Status,
                    Shipment_Id = t.Shipment_Id ?? "",
                    Do_Number = t.Do_Number ?? "",
                    LiquidLevel = t.LiquidLevel ?? 0,
                    WaterLevel = t.WaterLevel ?? 0,
                    LiquidTemperature = t.LiquidTemperature ?? 0.0,
                    TestTemperature = t.LiquidTemperature ?? 0.0,
                    LiquidDensity = t.LiquidDensity ?? 0.0,
                    Volume = t.Volume ?? 0.0,
                    SAP_Response = t.SAP_Response ?? ""
                }).ToList();

                Console.WriteLine("=== LoadData Completed ===");

                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsTotal,
                    recordsTotal = recordsTotal,
                    data = data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                return Json(new
                {
                    draw = 0,
                    recordsFiltered = 0,
                    recordsTotal = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        // ✅ Helper method untuk convert StatusReservasi ke text
        private string GetOperationTypeText(int? statusReservasi)
        {
            return statusReservasi switch
            {
                1 => "PSC",
                2 => "Routine Dipping",
                3 => "(TT) TANK TO TANK TRANSFER",
                4 => "(ROT) RECEIPT OTHERS",
                5 => "(ILS) ISSUE TO LSTK",
                6 => "(PI) PHYSICAL INVENTORY",
                7 => "(CHK) STOCK CHECKING",
                8 => "(UDG) UP/DOWN GRADATION",
                9 => "(BLD) BLENDING",
                _ => "-"
            };
        }

        //get: tankticket/addoredit
        [NoDirectAccess]

        public async Task<IActionResult> AddOrEdit(long id = 0)
        {
            if (id == 0)
            {
                populateTanks();
                populateTankTicketType();
                return View(new TankTicket());
            }

            else
            {
                populateTanks();
                populateTankTicketType();
                var tankticket = await _context.Tank_Ticket.FindAsync(id);
                if (tankticket == null)
                {
                    return NotFound();
                }
                return View(tankticket);
            }
        }

        //get: tankticket/addoredit
        [NoDirectAccess]
        public async Task<IActionResult> EditTankTicket(long id = 0)
        {
            populateTanks();
            populateTankTicketType();

            var tankticket = await _context.Tank_Ticket.FindAsync(id);
            if (tankticket == null)
            {
                return NotFound();
            }

            var editTankticket = new EditTankTicket
            {
                Ticket_ID = tankticket.Id,
                Ticket_Number = tankticket.Ticket_Number,
                Do_Number = tankticket.Do_Number,
                Shipment_Id = tankticket.Shipment_Id, // ✅ CORRECT property name
                Tank_Id = tankticket.Tank_Id,
                Operation_Type = tankticket.Operation_Type,
                Operation_Status = tankticket.Operation_Status,
                Tank = tankticket.tank,
                Date = Convert.ToDateTime(tankticket.Timestamp).ToString("dd MMM yyyy"),
                Time = Convert.ToDateTime(tankticket.Timestamp).ToString("HH:mm"),
                LiquidLevel = (tankticket.LiquidLevel.HasValue
                    ? tankticket.LiquidLevel.Value.ToString("F2")
                    : "0.00").Replace(",", "."),
                WaterLevel = (tankticket.WaterLevel.HasValue
                    ? tankticket.WaterLevel.Value.ToString("F2")
                    : "0.00").Replace(",", "."),
                LiquidTemperature = (tankticket.LiquidTemperature.HasValue
                    ? tankticket.LiquidTemperature.Value.ToString("F2")
                    : "0.00").Replace(",", "."),
                TestTemperature = tankticket.TestTemperature.ToString("F2").Replace(",", "."),
                LiquidDensity = (tankticket.LiquidDensity.HasValue
                    ? tankticket.LiquidDensity.Value.ToString("F3")
                    : "0.000").Replace(",", ".")
            };

            return View(editTankticket);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tankticket"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTankTicket(long id, [Bind("Ticket_ID,Tank_Id,Ticket_Number,Do_Number,Date,Time, LiquidLevel,WaterLevel,LiquidTemperature,TestTemperature,LiquidDensity")] EditTankTicket editTank)
        {
            // ✅ AUTHORIZATION: Check if user has permission to edit tank tickets
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            var updatetankTicket = await _context.Tank_Ticket.FirstOrDefaultAsync(t => t.Id == id);
            if (updatetankTicket != null)
            {
                updatetankTicket.Timestamp = Convert.ToDateTime(editTank.Date + " " + editTank.Time);
                updatetankTicket.LiquidLevel = (int?)Convert.ToInt32(Convert.ToDouble(editTank.LiquidLevel));
                updatetankTicket.WaterLevel = (int?)Convert.ToInt32(Convert.ToDouble(editTank.WaterLevel));
                updatetankTicket.LiquidTemperature = Convert.ToDouble(editTank.LiquidTemperature, CultureInfo.InvariantCulture);
                updatetankTicket.TestTemperature = Convert.ToDouble(editTank.TestTemperature, CultureInfo.InvariantCulture);
                updatetankTicket.LiquidDensity = Convert.ToDouble(editTank.LiquidDensity, CultureInfo.InvariantCulture);
                updatetankTicket.Is_Upload_Success = false;
                updatetankTicket.SAP_Response = "";
                updatetankTicket.Updated_By = User.Identity.Name;
                updatetankTicket.Updated_Timestamp = DateTime.Now;
                _context.Update(updatetankTicket);
                await _context.SaveChangesAsync();


                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_viewAll", _context.Tank_Ticket.ToList()) });
            }
            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "EditTankTicket", editTank) });
        }
        //get: tankticket/addoredit
        [NoDirectAccess]

        public async Task<IActionResult> AddTankTicket(long id = 0)
        {
            if (id == 0)
            {
                populateTanks();
                populateTankTicketType();
                return View(new TankTicket());
            }

            else
            {
                populateTanks();
                populateTankTicketType();
                var tankticket = await _context.Tank_Ticket.FindAsync(id);
                if (tankticket == null)
                {
                    return NotFound();
                }
                return View(tankticket);
            }
        }
        //get: tankticket/addoredit
        [NoDirectAccess]

        public async Task<IActionResult> AddNewTankTicket(long id = 0)
        {
            if (id == 0)
            {
                populateTanks();
                populateTankTicketType();
                return View(new NewTankTicket());
            }

            else
            {
                populateTanks();
                populateTankTicketType();
                var tank = await _context.Tank.FirstOrDefaultAsync(t => t.Tank_Name == id.ToString());
                if (tank == null)
                {
                    return NotFound();
                }
                var newTankTicket = new NewTankTicket();
                newTankTicket.Tank = tank;

                return View(newTankTicket);
            }
        }

        //To protect from overposting attacks, enable the specific properties you want to bind to
        //for more details, see http://go.microsoft.com/fwlink/?LinkId=317598
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(long id, [Bind("Id,Tank_Id,Timestamp,TicketNumber,Operation_Type,Operation_Status,Measurement_Method,Ticket_Number,Do_Number,Shipment_Id,LiquidLevel,WaterLevel,LiquidTemperature,LiquidDensity")] TankTicket tankticket)
        {
            // ✅ AUTHORIZATION: Check if user has permission to create/update tank tickets
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            if (ModelState.IsValid)
            {
                if (id == 0)
                {
                    Tank tank = new Tank();
                    tank.Tank_Name = tankticket.Tank_Number;

                    //tankticket.Id = new TankTicket()//
                    string datetime = tankticket.Timestamp.ToString();
                    tankticket.Timestamp = DateTime.Parse(datetime);
                    tankticket.Created_By = User.Identity.Name;
                    tankticket.Updated_Timestamp = DateTime.Now;
                    tankticket.Ticket_Number = tankticket.Operation_Type + GetTankNumber(tank.Tank_Name) + "/" + GetOperationStatus(tankticket.Operation_Status) + "/TANK-TICKET/BAAI/" + DateTime.Now.ToString("HHmmss") + DateTime.Now.ToString("/dd") + "/" + MonthFormat(DateTime.Now.ToString("MM")) + DateTime.Now.ToString("/yyyy");
                    _context.Add(tankticket);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    try
                    {
                        Tank tank = new Tank();
                        tank.Tank_Name = tankticket.Tank_Number;

                        string datetime = tankticket.Timestamp.ToString();
                        tankticket.Timestamp = DateTime.Parse(datetime);
                        tankticket.Updated_By = User.Identity.Name;
                        tankticket.Updated_Timestamp = DateTime.Now;
                        _context.Update(tankticket);
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!TankTicketExist(tankticket.Id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_viewAll", _context.Tank_Ticket.ToList()) });
            }
            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", tankticket) });
        }

        //Post: tankticket/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // ✅ AUTHORIZATION: Check if user has permission to delete tank tickets
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            var tankticket = await _context.Tank_Ticket.FindAsync(id);
            _context.Tank_Ticket.Remove(tankticket);
            await _context.SaveChangesAsync();
            return Json(new { html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Tank_Ticket.ToList()) });
        }
        private bool TankTicketExist(long id)
        {
            return _context.Tank_Ticket.Any(t => t.Id == id);
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
            ticketTypes.Add(new TicketType() { Operation = "IST", Description = "(IST) ISSUE TO STOCK TRANSFER" });
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
        /// <summary>
        /// Get data tank
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timerequest"></param>
        /// <returns></returns>
        public IActionResult GetData(long id, DateTime timerequest)
        {
            var requestTimeString = timerequest.ToString("MM/dd/yyyy HH:mm"); //convert time request to string

            // Get tank name from id
            var tankInfo = _context.Tank.FirstOrDefault(t => t.Tank_Name == id.ToString());
            if (tankInfo == null)
            {
                return Json(new TankHistorical());
            }

            var historical = _context.Tank_Historical
                .Where(t => t.Tank_Number == tankInfo.Tank_Name && t.TimeStamp.Value.Year == timerequest.Year && t.TimeStamp.Value.Month == timerequest.Month && t.TimeStamp.Value.Day == timerequest.Day && t.TimeStamp.Value.Hour == timerequest.Hour && t.TimeStamp.Value.Minute == timerequest.Minute)
                .OrderByDescending(t => t.TimeStamp).FirstOrDefault();

            if (historical == null)
            {
                return Json(new TankHistorical());
            }
            var tank = tankInfo;
            //historical.LiquidDensity = tank.IsManualDensity == true ? tank.ManualLabDensity : historical.LiquidDensity;
            //historical.LiquidTemperature = tank.IsManualTemp == true ? tank.ManualTemp : historical.LiquidTemperature;
            //historical.LiquidTemperature = tank.IsManualTestTemp == true ? tank.ManualTestTemp : historical.TestTemperature;
            //historical.WaterLevel = tank.IsManualWaterLevel == true ? tank.ManualWaterLevel : historical.WaterLevel;
            return Json(historical);
        }

        public IActionResult PrintTankTicket(long id)
        {
            //read configuration
            AppSettings appSettings = new AppSettings();
            AppSettings tankConfig = new AppSettings();
            appSettings = _configuration.GetSection("AppSettings").Get<AppSettings>();
            tankConfig = _configuration.GetSection("TankConfiguration").Get<AppSettings>();
            string logoPath = appSettings.LogoPath;
            int plantCode = tankConfig.PlantCode;

            TankTicket tankTicket = _context.Tank_Ticket.Include(t => t.tank).FirstOrDefault(t => t.Id == id);

            // ✅ FIX: Create instance dengan nama variable yang berbeda dari nama class
            var pdfRenderer = new TankTicketPdfRenderer();

            byte[] DownloadFile = pdfRenderer.Render(tankTicket, logoPath, plantCode);

            // Return the PDF file as a download
            return File(DownloadFile, "application/pdf", $"TankTicket # {tankTicket.Ticket_Number}.pdf");
        }

        public IActionResult PrintAllTank(TankTicketFilter filter)
        {
            try
            {
                //read configuration
                AppSettings appSettings = new AppSettings();
                AppSettings tankConfig = new AppSettings();
                appSettings = _configuration.GetSection("AppSettings").Get<AppSettings>();
                tankConfig = _configuration.GetSection("TankConfiguration").Get<AppSettings>();
                string logoPath = appSettings.LogoPath;
                int plantCode = tankConfig.PlantCode;

                var tickets = _context.Tank_Ticket.Include(t => t.tank)
                    .Where(t => t.Timestamp >= Convert.ToDateTime(filter.minDateTime) &&
                               t.Timestamp <= Convert.ToDateTime(filter.maxDateTime).AddHours(24));

                if (!string.IsNullOrEmpty(filter.TankNameFilter) && filter.TankNameFilter != "0")
                {
                    tickets = tickets.Where(t => t.Tank_Number == filter.TankNameFilter);
                }

                if (filter.OperationType != "--All Operation Type--")
                {
                    tickets = tickets.Where(t => t.Operation_Type == filter.OperationType);
                }

                if (filter.OperationStatus != 0)
                {
                    tickets = tickets.Where(t => t.Operation_Status == filter.OperationStatus);
                }

                var listTickets = tickets.ToList();

                foreach (var ticket in listTickets)
                {
                    if (ticket.tank == null)
                    {
                        ticket.tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == ticket.Tank_Number);
                    }
                }

                if (listTickets.Count > 0)
                {
                    // ✅ FIX: Create instance dengan nama variable yang berbeda
                    var pdfRenderer = new TankTicketPdfRenderer();

                    byte[] DownloadFile = pdfRenderer.RenderAll(listTickets, logoPath, plantCode);

                    return File(DownloadFile, "application/pdf", $"TankTicket # all.pdf");
                }
                return null;
            }
            catch
            {
                throw;
            }
        }

        //POST : Tankticket by ID
        public IActionResult PostData(long id)
        {
            TMS.SAP.TankDipPosting.ResponseCode _responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
            var ticket = _context.Tank_Ticket.FirstOrDefault(t => t.Id == id);
            if (ticket != null)
            {
                // ✅ Skip posting for PSC (1) and Routine Dipping (2)
                if (ticket.StatusReservasi == 1 || ticket.StatusReservasi == 2)
                {
                    _responseCode = new TMS.SAP.TankDipPosting.ResponseCode
                    {
                        Type = "W",
                        Code = "SKIP",
                        DescMsg = "PSC/RD tidak di-post ke SAP"
                    };
                    return Json(_responseCode);
                }

                // ✅ Skip if already synchronized
                if (ticket.Is_Upload_Success == true)
                {
                    _responseCode = new TMS.SAP.TankDipPosting.ResponseCode
                    {
                        Type = "W",
                        Code = "SKIP",
                        DescMsg = "Ticket sudah ter-sinkronisasi"
                    };
                    return Json(_responseCode);
                }

                _responseCode = UploadingTicket(ticket);
                if (_responseCode.Type == "S")
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = _responseCode.DescMsg;

                }
                else if (_responseCode.Type == "E")
                {
                    ticket.Is_Upload_Success = false;
                    ticket.SAP_Response = _responseCode.DescMsg;
                    //_context.Remove(ticket);
                }
                _context.Update(ticket);
                _context.SaveChangesAsync();
            }
            return Json(_responseCode);
        }

        /// <summary>
        /// Bulk post all unsynchronized tickets to SAP
        /// Similar to FDM's UploadTankDip() method
        /// </summary>
        [HttpPost]
        public IActionResult PostAllUnsynchronizedTickets()
        {
            try
            {
                // Find all unsynchronized tickets (excluding PSC and RD)
                var unsyncTickets = _context.Tank_Ticket
                    .Where(t => (t.Is_Upload_Success == null || t.Is_Upload_Success == false) &&
                               t.StatusReservasi.HasValue &&
                               t.StatusReservasi.Value != 1 && // Exclude PSC
                               t.StatusReservasi.Value != 2)   // Exclude Routine Dipping
                    .OrderBy(t => t.Timestamp)
                    .ToList();

                if (unsyncTickets.Count == 0)
                {
                    return Json(new
                    {
                        isValid = true,
                        message = "Tidak ada ticket yang perlu di-post ke SAP",
                        successCount = 0,
                        failCount = 0,
                        skipCount = 0
                    });
                }

                int successCount = 0;
                int failCount = 0;
                int skipCount = 0;
                var results = new List<object>();

                foreach (var ticket in unsyncTickets)
                {
                    try
                    {
                        var responseCode = UploadingTicket(ticket);

                        if (responseCode.Type == "S")
                        {
                            ticket.Is_Upload_Success = true;
                            ticket.SAP_Response = responseCode.DescMsg;
                            _context.Update(ticket);
                            successCount++;

                            results.Add(new
                            {
                                ticketNumber = ticket.Ticket_Number,
                                status = "SUCCESS",
                                message = responseCode.DescMsg
                            });
                        }
                        else
                        {
                            ticket.Is_Upload_Success = false;
                            ticket.SAP_Response = responseCode.DescMsg;
                            _context.Update(ticket);
                            failCount++;

                            results.Add(new
                            {
                                ticketNumber = ticket.Ticket_Number,
                                status = "FAILED",
                                message = responseCode.DescMsg
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = $"Exception: {ex.Message}";
                        _context.Update(ticket);
                        failCount++;

                        results.Add(new
                        {
                            ticketNumber = ticket.Ticket_Number,
                            status = "ERROR",
                            message = ex.Message
                        });
                    }
                }

                _context.SaveChanges();

                return Json(new
                {
                    isValid = true,
                    message = $"Posting selesai: {successCount} sukses, {failCount} gagal",
                    successCount,
                    failCount,
                    skipCount,
                    results
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    isValid = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        public string PostTankTicket(TankTicket ticket)
        {
            TMS.SAP.TankDipPosting.ResponseCode _responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
            string message = "";
            try
            {
                if (ticket != null)
                {
                    // ✅ Skip posting for PSC (1) and Routine Dipping (2) - they should not be posted to SAP
                    if (ticket.StatusReservasi == 1 || ticket.StatusReservasi == 2)
                    {
                        message = $"Skipped posting {ticket.Ticket_Number} - PSC/RD tidak di-post ke SAP";
                        return message;
                    }

                    // ✅ Skip if already synchronized
                    if (ticket.Is_Upload_Success == true)
                    {
                        message = $"Skipped posting {ticket.Ticket_Number} - sudah ter-sinkronisasi";
                        return message;
                    }

                    _responseCode = UploadingTicket(ticket);
                    if (_responseCode.Type == "S")
                    {
                        message = "Success Post " + ticket.Ticket_Number + " " + _responseCode.DescMsg;
                        ticket.Is_Upload_Success = true;
                        ticket.SAP_Response = _responseCode.DescMsg;

                        // ✅ Parse additionalQuantity dari SAP response untuk mendapatkan nilai volume
                        ParseAndUpdateVolumeFromSAPResponse(ticket, _responseCode);

                        _context.Update(ticket);
                    }
                    else if (_responseCode.Type == "E")
                    {
                        message = "Failed Post " + ticket.Ticket_Number + " " + _responseCode.DescMsg;

                        // ✅ Tetap parse additionalQuantity meskipun error (SAP kadang tetap return nilai)
                        if (_responseCode.additionalQuantity != null && _responseCode.additionalQuantity.Length > 0)
                        {
                            ParseAndUpdateVolumeFromSAPResponse(ticket, _responseCode);
                        }

                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = _responseCode.DescMsg;
                        _context.Remove(ticket);
                        _context.SaveChanges();
                        throw new Exception(message);

                    }
                    _context.SaveChanges();
                }
                return message;
            }
            catch (Exception ex)
            {
                if (_responseCode.Type == "E")
                {
                    throw new Exception(ex.Message);
                }
                else
                {
                    throw new Exception("Connection to MySAP Failed !!! Please Check Networking");
                }
            }
        }
        //process uploading tankticket
        private TMS.SAP.TankDipPosting.ResponseCode UploadingTicket(TankTicket ticket)
        {
            TMS.SAP.TankDipPosting.ResponseCode responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
            //read configuration
            TankConfiguration tankConfiguration = new TankConfiguration();
            tankConfiguration = _configuration.GetSection("TankConfiguration").Get<TankConfiguration>();
            string url = tankConfiguration.UrlPosting;
            Tank tank = _context.Tank.FirstOrDefault(t => t.Tank_Name == ticket.Tank_Number);
            #region Initial value
            TMS.SAP.TankDipPosting.DippingDataDelivery data = new TMS.SAP.TankDipPosting.DippingDataDelivery();
            data.plant = tankConfiguration.PlantCode;
            data.dipDate = ((DateTime)ticket.Timestamp).ToString("ddMMyyyy");
            data.dipTime = ((DateTime)ticket.Timestamp).ToString("HHmm");
            data.dipTank = ConvertTankNumberToSAPTankNumber(tank.Tank_Name);
            data.dipEvent = ConvertStatusToDipEvent(ticket.Operation_Status);
            data.dipOperation = ConvertStatusReservasiToDipOperation(ticket.StatusReservasi);
            data.totalHeight = (ticket.LiquidLevel ?? 0).ToString();
            data.totalHeightUOM = "MM";
            data.waterHeight = (ticket.WaterLevel ?? 0).ToString();
            data.waterHeightUOM = "MM";
            data.celciusMaterialTemp = ticket.LiquidTemperature == 0 ? "0" : (ticket.LiquidTemperature.HasValue ? ticket.LiquidTemperature.Value.ToString("0.00") : "0.00");
            data.celciusMaterialTemp = data.celciusMaterialTemp.Replace('.', ',');
            data.celciusTestTemp = ticket.TestTemperature == 0 ? "0" : ticket.TestTemperature.ToString("0.00");
            data.celciusTestTemp = data.celciusTestTemp.Replace('.', ',');
            data.kglDensity = ticket.LiquidDensity == 0 ? "0" : (ticket.LiquidDensity.HasValue ? ticket.LiquidDensity.Value.ToString("0.0000") : "0.0000");
            data.kglDensity = data.kglDensity.Replace('.', ',');
            data.delivery = ticket.Do_Number;
            data.shipment = ticket.Shipment_Id;
            #endregion
            //create service
            string password = string.Format("{0}{1}{2}{3}", data.plant, data.dipDate, data.dipTime, data.dipOperation);
            //request 
            TMS.SAP.TankDipPosting.DoPostingRequestBody Body = new TMS.SAP.TankDipPosting.DoPostingRequestBody();
            Body.dipData = data;
            Body.User = data.plant;
            Body.Password = password;
            TMS.SAP.TankDipPosting.DoPostingRequest request = new TMS.SAP.TankDipPosting.DoPostingRequest();
            request.Body = Body;

            //Do Posting
            TMS.SAP.TankDipPosting.ZMM_TANKDIP_DELIVERYSoapClient.EndpointConfiguration endpoint = new TMS.SAP.TankDipPosting.ZMM_TANKDIP_DELIVERYSoapClient.EndpointConfiguration();
            TMS.SAP.TankDipPosting.ZMM_TANKDIP_DELIVERYSoapClient service = new TMS.SAP.TankDipPosting.ZMM_TANKDIP_DELIVERYSoapClient(endpoint, url);
            TMS.SAP.TankDipPosting.DoPostingResponse response = new TMS.SAP.TankDipPosting.DoPostingResponse();
            response = service.DoPosting(request);
            //respon byd
            TMS.SAP.TankDipPosting.DoPostingResponseBody responseBody = response.Body;
            responseCode = responseBody.DoPostingResult;
            return responseCode;

        }
        private string ConvertTankNumberToSAPTankNumber(string tanknumber)
        {
            string[] splits = tanknumber.Split('-');
            int tanknum = Convert.ToInt32(splits[1]);

            return "T" + tanknum.ToString("000");
        }
        private string ConvertStatusToDipEvent(int? status)
        {
            if (!status.HasValue)
                return "";

            if (status.Value == 1)
                return "O";

            if (status.Value == 2)
                return "C";
            if (status.Value == 0)
                return "C";

            return "";
        }

        /// <summary>
        /// Converts StatusReservasi to SAP DipOperation code
        /// Based on TankTicket.Operation_Type mapping
        /// </summary>
        private string ConvertStatusReservasiToDipOperation(int? statusReservasi)
        {
            if (!statusReservasi.HasValue)
                return "";

            return statusReservasi.Value switch
            {
                1 => "PSC",     // PSC - tidak di-post ke SAP
                2 => "RD",      // Routine Dipping - tidak di-post ke SAP
                3 => "TT",      // Tank to Tank Transfer
                4 => "ROT",     // Receipt Others
                5 => "ILS",     // Issue to LSTK/Sales
                6 => "PI",      // Physical Inventory
                7 => "CHK",     // Stock Checking
                8 => "UDG",     // Up/Down Gradation
                9 => "BLD",     // Blending
                _ => ""
            };
        }

        /// <summary>
        /// Parse additionalQuantity dari SAP response dan update nilai volume ke TankTicket
        /// UOM codes dari SAP:
        /// - BB6 = Barrel @ 60°F
        /// - L   = Liter (Volume Product/Observed)
        /// - L15 = Liter @ 15°C (Standard Volume)
        /// - LTO = Long Ton
        /// - MT  = Metric Ton (tidak disimpan)
        /// </summary>
        private void ParseAndUpdateVolumeFromSAPResponse(TankTicket ticket, TMS.SAP.TankDipPosting.ResponseCode responseCode)
        {
            if (responseCode.additionalQuantity == null || responseCode.additionalQuantity.Length == 0)
            {
                Console.WriteLine($"[ParseVolume] SAP response tidak memiliki additionalQuantity untuk ticket {ticket.Ticket_Number}");
                return;
            }

            foreach (var quantity in responseCode.additionalQuantity)
            {
                if (string.IsNullOrEmpty(quantity.Uom) || string.IsNullOrEmpty(quantity.Qty))
                    continue;

                // Parse quantity value (SAP menggunakan koma sebagai decimal separator)
                string qtyString = quantity.Qty.Replace(',', '.');
                if (!double.TryParse(qtyString, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    Console.WriteLine($"[ParseVolume] Gagal parse quantity value '{quantity.Qty}' untuk UOM '{quantity.Uom}'");
                    continue;
                }

                switch (quantity.Uom.ToUpper())
                {
                    case "BB6":
                        // Barrel @ 60°F
                        ticket.VolumeBarrel60F = val;
                        Console.WriteLine($"[ParseVolume] Volume Barrel60F: {val}");
                        break;

                    case "L":
                        // Volume Product (Observed Volume in Liters)
                        ticket.Volume = val;
                        Console.WriteLine($"[ParseVolume] Volume Product: {val}");
                        break;

                    case "L15":
                        // Volume @ 15°C (Standard Volume)
                        ticket.Volume15C = val;
                        Console.WriteLine($"[ParseVolume] Volume 15C: {val}");
                        break;

                    case "LTO":
                        // Long Ton
                        ticket.VolumeLongTon = val;
                        Console.WriteLine($"[ParseVolume] Volume LongTon: {val}");
                        break;

                    case "MT":
                        // Metric Ton - tidak disimpan di model saat ini
                        Console.WriteLine($"[ParseVolume] Metric Ton (not stored): {val}");
                        break;

                    default:
                        Console.WriteLine($"[ParseVolume] Unknown UOM '{quantity.Uom}' dengan nilai {val}");
                        break;
                }
            }

            Console.WriteLine($"[ParseVolume] Parsed {responseCode.additionalQuantity.Length} quantity values dari SAP untuk ticket {ticket.Ticket_Number}");
        }

        private string GetTankNumber(string tankName)
        {
            var val = _context.Tank.Where(t => t.Tank_Name == tankName).Select(t => t.Tank_Name).FirstOrDefault();
            val = val.Substring(val.Length - 2, 2);
            return val;
        }

        private string GetOperationStatus(int val)
        {
            var result = "";
            if (val == 1)
            {
                result = "OPEN";
            }
            else
            {
                result = "CLOSE";
            }

            return result;
        }

        private string MonthFormat(string month)
        {
            var val = "";
            if (month != null)
            {
                if (month == "01")
                {
                    val = "I";
                }
                else if (month == "02")
                {
                    val = "II";
                }
                else if (month == "03")
                {
                    val = "III";
                }
                else if (month == "04")
                {
                    val = "IV";
                }
                else if (month == "05")
                {
                    val = "V";
                }
                else if (month == "06")
                {
                    val = "VI";
                }
                else if (month == "07")
                {
                    val = "VII";
                }
                else if (month == "08")
                {
                    val = "VIII";
                }
                else if (month == "09")
                {
                    val = "IX";
                }
                else if (month == "10")
                {
                    val = "X";
                }
                else if (month == "11")
                {
                    val = "XI";
                }
                else if (month == "12")
                {
                    val = "XII";
                }
            }
            return val;
        }

        private string TicketRenderer(string type, int cntTicket)
        {

            string[] validTypes = { "BLD", "DL", "ICR", "ILS", "IOT", "IS", "IST", "OWN", "PI", "RCR", "ROT", "RPR", "TPL", "TRF", "TT", "UDG", "CHK" };
            if (!validTypes.Contains(type))
            {
                return null;
            }

            DateTime now = DateTime.Now;
            int currentYear = now.Year;
            int currentMonth = now.Month;

            cntTicket += 1;

            string ticket = $"{type}/{currentYear:0000}{currentMonth:00}{cntTicket:0000}";
            return ticket;
        }

        /// <summary>
        /// ✅ FIX: Changed to accept StatusReservasi instead of Operation_Type
        /// Operation_Type is [NotMapped] computed property, can't be queried from database
        /// </summary>
        private int GetNumberTicket(int? statusReservasi)
        {
            if (!statusReservasi.HasValue)
                return 0;

            DateTime now = DateTime.Now;
            int currentYear = now.Year;
            int currentMonth = now.Month;

            int countTicket = _context.Tank_Ticket
               .Where(t => t.StatusReservasi == statusReservasi &&
                          t.Timestamp.HasValue &&
                          t.Timestamp.Value.Year == currentYear)
               .Count();

            return countTicket;
        }

        // Tank Ticket Export To Excel - Get ALL data based on filters
        [HttpPost]
        public IActionResult ExportToExcel(TankTicketExportFilter filter)
        {
            try
            {
                // Handle null filter
                if (filter == null)
                {
                    Console.WriteLine("Export Excel - Filter is NULL, using default (no filters)");
                    filter = new TankTicketExportFilter();
                }

                Console.WriteLine($"Export Excel - Tank: {filter.TankName ?? "null"}, OpType: {filter.OperationType ?? "null"}, OpStatus: {filter.OperationStatus?.ToString() ?? "null"}");
                Console.WriteLine($"Export Excel - Ticket: {filter.TicketNumber ?? "null"}");
                Console.WriteLine($"Export Excel - DateFrom: {filter.DateFrom?.ToString() ?? "null"}, DateTo: {filter.DateTo?.ToString() ?? "null"}");

                // Query database with filters - GET ALL DATA, not just current page!
                var query = _context.Tank_Ticket.AsQueryable();

                // Apply Tank Name filter
                if (!string.IsNullOrEmpty(filter.TankName) && filter.TankName != "0" && filter.TankName != "---- All Tank ----")
                {
                    query = query.Where(t => t.Tank_Number == filter.TankName);
                }

                // Apply Operation Type filter - support both short code and full description
                if (!string.IsNullOrEmpty(filter.OperationType) && filter.OperationType != "0" && filter.OperationType != "--All Operation Type--")
                {
                    // Map short code to StatusReservasi for filtering
                    int? statusReservasi = filter.OperationType?.ToUpper() switch
                    {
                        "PSC" => 1,
                        "ROUTINE DIPPING" or "RD" or "DL" => 2,
                        "TT" => 3,
                        "ROT" => 4,
                        "ILS" or "IS" or "ICR" => 5,
                        "PI" => 6,
                        "CHK" => 7,
                        "UDG" => 8,
                        "BLD" => 9,
                        _ => null
                    };

                    if (statusReservasi.HasValue)
                    {
                        query = query.Where(t => t.StatusReservasi == statusReservasi.Value);
                    }
                }

                // Apply Operation Status filter
                if (filter.OperationStatus.HasValue && filter.OperationStatus.Value != 0)
                {
                    query = query.Where(t => t.Operation_Status == filter.OperationStatus.Value);
                }

                // Apply Ticket Number filter
                if (!string.IsNullOrEmpty(filter.TicketNumber))
                {
                    query = query.Where(t => t.Ticket_Number.Contains(filter.TicketNumber));
                }

                // Apply Date filters
                if (filter.DateFrom.HasValue)
                {
                    query = query.Where(t => t.Timestamp >= filter.DateFrom.Value);
                }

                if (filter.DateTo.HasValue)
                {
                    DateTime dateToAdjusted = filter.DateTo.Value.AddDays(1).AddHours(0).AddMinutes(-1);
                    query = query.Where(t => t.Timestamp <= dateToAdjusted);
                }

                // Get ALL filtered data (no paging!)
                var tableData = query.OrderByDescending(t => t.Timestamp).ToList();

                Console.WriteLine($"Export Excel - Total tickets found: {tableData.Count}");

                // Open the template file
                FileStream templateStream = new FileStream("Doc/TemplateTankTicket.xls", FileMode.Open, FileAccess.Read);

                // Create a new workbook object based on the template file
                HSSFWorkbook workbook = new HSSFWorkbook(templateStream);

                // Get the sheet you want to write data to
                ISheet sheet = workbook.GetSheet("TankTicket");

                // Set Date Export
                IRow rowDate = sheet.CreateRow(5);
                rowDate.CreateCell(1).SetCellValue("Date Export : ");
                rowDate.CreateCell(2).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

                // Load tank data
                foreach (var t in tableData)
                {
                    Tank tank = _context.Tank.Where(p => p.Tank_Name == t.Tank_Number).FirstOrDefault();
                    t.tank = tank;
                }

                // Write the data model to the cells in the sheet
                int rowIndex = 9;
                int number = 1;
                // Start at row 1 (0-based index) to skip the header row
                foreach (var ticket in tableData)
                {

                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue(number);
                    row.CreateCell(1).SetCellValue(ticket.tank.Tank_Name);
                    row.CreateCell(2).SetCellValue(ticket.Ticket_Number);
                    row.CreateCell(3).SetCellValue(Convert.ToDateTime(ticket.Timestamp).ToString("dd MMMM yyyy HH:mm:ss"));
                    row.CreateCell(4).SetCellValue(ticket.Operation_Type);
                    row.CreateCell(5).SetCellValue(ticket.Operation_Status == 1 ? "Open" : (ticket.Operation_Status == 2 ? "Close" : ""));
                    row.CreateCell(6).SetCellValue((double)((int)(ticket.LiquidLevel ?? 0)));
                    row.CreateCell(7).SetCellValue((double)((int)(ticket.WaterLevel ?? 0)));
                    row.CreateCell(8).SetCellValue((double)(ticket.LiquidTemperature ?? 0));
                    row.CreateCell(9).SetCellValue(ticket.TestTemperature);
                    // Format Density with 3 decimal places
                    var densityCell = row.CreateCell(10);
                    densityCell.SetCellValue(Math.Round(ticket.LiquidDensity ?? 0, 3));
                    var densityStyle = workbook.CreateCellStyle();
                    var densityFormat = workbook.CreateDataFormat();
                    densityStyle.DataFormat = densityFormat.GetFormat("0.000");
                    densityCell.CellStyle = densityStyle;
                    row.CreateCell(11).SetCellValue(ParseSAPResponse(ticket.SAP_Response));
                    rowIndex++;
                    number++;
                }

                // Save the workbook to a new file
                MemoryStream stream = new MemoryStream();
                workbook.Write(stream);
                stream.Position = 0;
                string time = DateTime.Now.ToString("yyyy MMMM dd HH:MM:ss");
                FileStreamResult file = new FileStreamResult(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                file.FileDownloadName = $"{time} Export Tank Ticket.xls";

                // Close the streams
                templateStream.Close();

                return file;
            }
            catch
            {
                throw;
            }

        }

        #region Helper Methods for AddOpenCloseTicket

        /// <summary>
        /// Parse SAP Response untuk display
        /// </summary>
        private string ParseSAPResponse(string sapResponse)
        {
            if (string.IsNullOrEmpty(sapResponse))
                return "-";
            return sapResponse;
        }

        /// <summary>
        /// Generate Ticket Number berdasarkan Operation Type dan Status
        /// </summary>
        private string GenerateTicketNumber(string operationType, int operationStatus)
        {
            // Extract operation code from operation type (e.g., "(ROT) RECEIPT OTHERS" -> "ROT")
            var opCode = operationType?.ToUpper() ?? "";
            if (opCode.Contains("(") && opCode.Contains(")"))
            {
                var startIndex = opCode.IndexOf('(') + 1;
                int endIndex = opCode.IndexOf(')');
                if (endIndex > startIndex)
                {
                    opCode = opCode.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            // Map operation code to StatusReservasi
            int? statusReservasi = opCode switch
            {
                "PSC" => 1,
                "ROUTINE DIPPING" or "RD" or "DL" => 2,
                "TT" => 3,
                "ROT" => 4,
                "ILS" or "IS" or "ICR" => 5,
                "PI" => 6,
                "CHK" => 7,
                "UDG" => 8,
                "BLD" => 9,
                _ => null
            };

            // Get ticket count for this operation type
            int countTicket = GetNumberTicket(statusReservasi);

            // Generate ticket number
            string statusStr = operationStatus == 1 ? "OPEN" : "CLOSE";
            string tankNumber = "XX"; // Default

            DateTime now = DateTime.Now;
            string ticketNumber = $"{opCode}/{now:yyyyMM}{(countTicket + 1):0000}/{statusStr}";

            return ticketNumber;
        }

        /// <summary>
        /// Parse DateTime from date and time strings
        /// </summary>
        private DateTime? ParseDateTime(string dateStr, string timeStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.Now;

            try
            {
                string dateTimeStr = $"{dateStr} {timeStr ?? "00:00"}";

                var formats = new[]
                {
                    "dd MMMM yyyy HH:mm",
                    "d MMMM yyyy HH:mm",
                    "dd MMMM yyyy HH:mm",
                    "d MMMM yyyy HH:mm",
                    "DD MMMM YYYY HH:mm:ss",
                    "D MMM YYYY HH:mm:ss",
                    "DD MMMM YYYY HH:mm",
                    "D MMMM YYYY HH:mm",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-dd HH:mm"
                };

                if (DateTime.TryParseExact(dateTimeStr, formats,
                    System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                    System.Globalization.DateTimeStyles.None,
                    out var result))
                {
                    return result;
                }

                // Fallback: try standard parse
                if (DateTime.TryParse(dateTimeStr, out var fallbackResult))
                {
                    return fallbackResult;
                }

                return DateTime.Now;
            }
            catch
            {
                return DateTime.Now;
            }
        }

        /// <summary>
        /// Parse string to int with null handling
        /// </summary>
        private int? ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Remove any non-numeric characters except minus sign
            value = value.Trim().Replace(",", ".");

            if (int.TryParse(value, out int result))
                return result;

            // Try parsing as double first then convert
            if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double dResult))
                return (int)dResult;

            return null;
        }

        /// <summary>
        /// Parse string to double with null handling
        /// </summary>
        private double? ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Normalize decimal separator
            value = value.Trim().Replace(",", ".");

            if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;

            return null;
        }

        #endregion

        /// <summary>
        /// ✅ Save Tank Ticket dari form AddNewTankTicket (NewTankTicket model)
        /// Menerima data OPEN atau CLOSE (satu per satu)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddOpenCloseTicket([FromBody] NewTankTicket model)
        {
            try
            {
                Console.WriteLine("=== AddOpenCloseTicket Called ===");
                Console.WriteLine($"Tank: {model.Tank_Number}");
                Console.WriteLine($"Operation_Type: {model.Operation_Type}");
                Console.WriteLine($"CheckStatusOpen: {model.CheckStatusOpen}");
                Console.WriteLine($"CheckStatusClose: {model.CheckStatusClose}");

                // Validate input
                if (string.IsNullOrEmpty(model.Tank_Number) || model.Tank_Number == "0")
                {
                    return Json(new { isValid = false, message = "Tank Number is required" });
                }

                if (string.IsNullOrEmpty(model.Operation_Type) || model.Operation_Type == "--All Operation Type--")
                {
                    return Json(new { isValid = false, message = "Operation Type is required" });
                }

                // Check which status is checked (OPEN or CLOSE)
                bool isOpen = model.CheckStatusOpen == "1";
                bool isClose = model.CheckStatusClose == "1";

                if (!isOpen && !isClose)
                {
                    return Json(new { isValid = false, message = "Please select OPEN or CLOSE status" });
                }

                // Map Operation_Type to StatusReservasi
                int? statusReservasi = model.Operation_Type?.ToUpper() switch
                {
                    "PSC" => 1,
                    "RD" or "ROUTINE DIPPING" => 2,
                    "TT" => 3,
                    "ROT" => 4,
                    "ILS" or "IS" or "ICR" => 5,
                    "PI" => 6,
                    "CHK" => 7,
                    "UDG" => 8,
                    "BLD" => 9,
                    _ => null
                };

                // ✅ FLAG: Untuk menampilkan modal threshold setting
                bool showThresholdModal = false;

                // Create ticket based on status
                if (isOpen)
                {
                    Console.WriteLine("Creating OPEN ticket...");

                    var openTicket = new TankTicket
                    {
                        Tank_Number = model.Tank_Number,
                        Ticket_Number = GenerateTicketNumber(model.Operation_Type, 1),
                        Timestamp = ParseDateTime(model.DateOpen, model.TimeOpen),
                        Operation_Status = 1, // OPEN
                        StatusReservasi = statusReservasi,
                        Shipment_Id = model.Shipment_Id,
                        Do_Number = model.Do_Number,
                        LiquidLevel = ParseInt(model.LiquidLevelOpen),
                        WaterLevel = ParseInt(model.WaterLevelOpen),
                        LiquidTemperature = ParseDouble(model.LiquidTemperatureOpen),
                        TestTemperature = ParseDouble(model.TestTemperatureOpen) ?? 0,
                        LiquidDensity = ParseDouble(model.LiquidDensityOpen),
                        Created_By = User.Identity?.Name,
                        Created_Timestamp = DateTime.Now,
                        Is_Upload_Success = false
                    };

                    _context.Tank_Ticket.Add(openTicket);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"OPEN ticket saved with ID: {openTicket.Id}");

                    // ✅ UPDATED: Jika ROT OPEN, tampilkan modal untuk set alarm
                    // Tank Movement TIDAK dibuat otomatis - hanya setelah user submit dari modal
                    if (model.Operation_Type?.ToUpper() == "ROT")
                    {
                        // ✅ SET FLAG: Show threshold modal untuk ROT OPEN
                        // User akan set target level dan threshold dari modal
                        // Tank Movement akan dibuat saat user submit dari modal
                        showThresholdModal = true;
                        Console.WriteLine($"[ROT OPEN] Showing threshold modal for {model.Tank_Number} - Tank Movement will be created on modal submit");
                    }

                    // Post to SAP if requested
                    if (model.IsPosting == "1")
                    {
                        try
                        {
                            var postResult = PostTankTicket(openTicket);
                            Console.WriteLine($"Post result: {postResult}");
                            return Json(new {
                                isValid = true,
                                message = postResult,
                                showThresholdModal = showThresholdModal,
                                tankNumber = model.Tank_Number,
                                tankTicketId = openTicket.Id
                            });
                        }
                        catch (Exception ex)
                        {
                            return Json(new {
                                isValid = false,
                                message = $"Ticket saved but posting failed: {ex.Message}",
                                showThresholdModal = showThresholdModal,
                                tankNumber = model.Tank_Number,
                                tankTicketId = openTicket.Id
                            });
                        }
                    }

                    return Json(new {
                        isValid = true,
                        message = "OPEN ticket saved successfully",
                        showThresholdModal = showThresholdModal,
                        tankNumber = model.Tank_Number,
                        tankTicketId = openTicket.Id
                    });
                }

                if (isClose)
                {
                    Console.WriteLine("Creating CLOSE ticket...");

                    var closeTicket = new TankTicket
                    {
                        Tank_Number = model.Tank_Number,
                        Ticket_Number = GenerateTicketNumber(model.Operation_Type, 2),
                        Timestamp = ParseDateTime(model.DateClose, model.TimeClose),
                        Operation_Status = 2, // CLOSE
                        StatusReservasi = statusReservasi,
                        Shipment_Id = model.Shipment_Id,
                        Do_Number = model.Do_Number,
                        LiquidLevel = ParseInt(model.LiquidLevelClose),
                        WaterLevel = ParseInt(model.WaterLevelClose),
                        LiquidTemperature = ParseDouble(model.LiquidTemperatureClose),
                        TestTemperature = ParseDouble(model.TestTemperatureClose) ?? 0,
                        LiquidDensity = ParseDouble(model.LiquidDensityClose),
                        Created_By = User.Identity?.Name,
                        Created_Timestamp = DateTime.Now,
                        Is_Upload_Success = false
                    };

                    _context.Tank_Ticket.Add(closeTicket);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"CLOSE ticket saved with ID: {closeTicket.Id}");

                    // ✅ AUTO-DEACTIVATE: Jika ROT CLOSE, deaktifkan Tank Movement
                    if (model.Operation_Type?.ToUpper() == "ROT")
                    {
                        var movement = _context.Tank_Movement.FirstOrDefault(m => m.Tank_Number == model.Tank_Number);
                        
                        if (movement != null)
                        {
                            movement.Status = 0;
                            movement.StagnantAlarm = false;
                            movement.EstimationTimeStamp = null;
                            _context.Update(movement);
                            await _context.SaveChangesAsync();
                            Console.WriteLine($"[ROT CLOSE] Tank Movement deactivated for {model.Tank_Number}");
                        }
                    }

                    // Post to SAP if requested
                    if (model.IsPosting == "1")
                    {
                        try
                        {
                            var postResult = PostTankTicket(closeTicket);
                            Console.WriteLine($"Post result: {postResult}");
                            return Json(new { isValid = true, message = postResult });
                        }
                        catch (Exception ex)
                        {
                            return Json(new { isValid = false, message = $"Ticket saved but posting failed: {ex.Message}" });
                        }
                    }

                    return Json(new { isValid = true, message = "CLOSE ticket saved successfully" });
                }

                return Json(new { isValid = false, message = "Unknown error" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AddOpenCloseTicket ERROR: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { isValid = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// ✅ Get live data from Tank_LiveData for form population
        /// Called when user clicks refresh button or checks OPEN/CLOSE checkbox
        /// </summary>
        [HttpPost]
        public IActionResult GetLiveDataForForm(string id, string timerequest)
        {
            try
            {
                Console.WriteLine($"[GetLiveDataForForm] Tank: {id}, TimeRequest: {timerequest}");

                if (string.IsNullOrEmpty(id) || id == "0" || id == "---- Select Tank ----" || id == "---- All Tank ----")
                {
                    return Json(new
                    {
                        liquidLevel = 0,
                        waterLevel = 0,
                        liquidTemperature = 0.0,
                        testTemperature = 0.0,
                        liquidDensity = 0.0
                    });
                }

                // Try to parse the time request
                DateTime requestTime = DateTime.Now;
                if (!string.IsNullOrEmpty(timerequest))
                {
                    var formats = new[]
                    {
                        "dd MMMM yyyy HH:mm:ss",
                        "d MMMM yyyy HH:mm:ss",
                        "dd MMMM yyyy HH:mm",
                        "d MMMM yyyy HH:mm",
                        "DD MMMM YYYY HH:mm:ss",
                        "D MMM YYYY HH:mm:ss",
                        "DD MMMM YYYY HH:mm",
                        "D MMMM YYYY HH:mm",
                        "yyyy-MM-dd HH:mm:ss",
                        "yyyy-MM-dd HH:mm"
                    };

                    if (!DateTime.TryParseExact(timerequest, formats,
                        System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                        System.Globalization.DateTimeStyles.None,
                        out requestTime))
                    {
                        // Fallback to standard parse
                        if (!DateTime.TryParse(timerequest, out requestTime))
                        {
                            requestTime = DateTime.Now;
                        }
                    }
                }

                Console.WriteLine($"[GetLiveDataForForm] Parsed time: {requestTime:yyyy-MM-dd HH:mm:ss}");

                // Check if request is for current time (within 2 minutes) - use LIVE data
                var timeDiff = Math.Abs((DateTime.Now - requestTime).TotalMinutes);
                bool useLiveData = timeDiff < 2;

                Console.WriteLine($"[GetLiveDataForForm] TimeDiff: {timeDiff:F1} min, UseLiveData: {useLiveData}");

                if (useLiveData)
                {
                    // Get LIVE data from Tank_LiveData
                    var liveData = _context.Tank_Live_Data
                        .AsNoTracking()
                        .FirstOrDefault(t => t.Tank_Number == id);

                    if (liveData != null)
                    {
                        // Get tank for manual overrides
                        var tank = _context.Tank.AsNoTracking().FirstOrDefault(t => t.Tank_Name == id);

                        var result = new
                        {
                            liquidLevel = liveData.Level ?? 0,
                            waterLevel = liveData.Level_Water ?? 0,
                            liquidTemperature = (tank?.IsManualTemp == true ? tank.ManualTemp : liveData.Temperature) ?? 0.0,
                            testTemperature = (tank?.IsManualTestTemp == true ? tank.ManualTestTemp : liveData.Temperature) ?? 0.0,
                            liquidDensity = (tank?.IsManualDensity == true ? tank.ManualLabDensity : liveData.Density) ?? 0.0
                        };

                        Console.WriteLine($"[GetLiveDataForForm] LIVE data: Level={result.liquidLevel}, Temp={result.liquidTemperature}, Density={result.liquidDensity}");
                        return Json(result);
                    }
                }
                else
                {
                    // Get HISTORICAL data from Tank_Historical
                    var historical = _context.Tank_Historical
                        .AsNoTracking()
                        .Where(t => t.Tank_Number == id &&
                                   t.TimeStamp.HasValue &&
                                   t.TimeStamp.Value.Year == requestTime.Year &&
                                   t.TimeStamp.Value.Month == requestTime.Month &&
                                   t.TimeStamp.Value.Day == requestTime.Day &&
                                   t.TimeStamp.Value.Hour == requestTime.Hour &&
                                   t.TimeStamp.Value.Minute == requestTime.Minute)
                        .OrderByDescending(t => t.TimeStamp)
                        .FirstOrDefault();

                    if (historical != null)
                    {
                        var result = new
                        {
                            liquidLevel = historical.Level ?? 0,
                            waterLevel = historical.Level_Water ?? 0,
                            liquidTemperature = historical.Temperature ?? 0.0,
                            testTemperature = historical.Temperature ?? 0.0,
                            liquidDensity = historical.Density ?? 0.0
                        };

                        Console.WriteLine($"[GetLiveDataForForm] HISTORICAL data: Level={result.liquidLevel}, Temp={result.liquidTemperature}");
                        return Json(result);
                    }
                    else
                    {
                        // If no exact match, try to get closest historical record
                        var closestHistorical = _context.Tank_Historical
                            .AsNoTracking()
                            .Where(t => t.Tank_Number == id &&
                                       t.TimeStamp.HasValue &&
                                       t.TimeStamp.Value >= requestTime.AddMinutes(-5) &&
                                       t.TimeStamp.Value <= requestTime.AddMinutes(5))
                            .OrderBy(t => Math.Abs((t.TimeStamp.Value - requestTime).TotalSeconds))
                            .FirstOrDefault();

                        if (closestHistorical != null)
                        {
                            var result = new
                            {
                                liquidLevel = closestHistorical.Level ?? 0,
                                waterLevel = closestHistorical.Level_Water ?? 0,
                                liquidTemperature = closestHistorical.Temperature ?? 0.0,
                                testTemperature = closestHistorical.Temperature ?? 0.0,
                                liquidDensity = closestHistorical.Density ?? 0.0
                            };

                            Console.WriteLine($"[GetLiveDataForForm] CLOSEST HISTORICAL: Level={result.liquidLevel}");
                            return Json(result);
                        }
                    }
                }

                // Fallback: Return zeros
                Console.WriteLine($"[GetLiveDataForForm] No data found, returning zeros");
                return Json(new
                {
                    liquidLevel = 0,
                    waterLevel = 0,
                    liquidTemperature = 0.0,
                    testTemperature = 0.0,
                    liquidDensity = 0.0
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLiveDataForForm] ERROR: {ex.Message}");
                return Json(new
                {
                    liquidLevel = 0,
                    waterLevel = 0,
                    liquidTemperature = 0.0,
                    testTemperature = 0.0,
                    liquidDensity = 0.0,
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request model untuk SetNoFlowrateAlarmThreshold
    /// </summary>
    public class NoFlowrateThresholdRequest
    {
        public string TankNumber { get; set; }
        public int ThresholdMinutes { get; set; }
    }
}