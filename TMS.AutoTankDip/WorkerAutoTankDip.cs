using EasyModbus;
using System.Text.Json;
using TMS.Models;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.IO.Ports;
using Microsoft.EntityFrameworkCore;
using TMS.TankDipPosting.Service;

namespace TMS.TankDipPosting
{
    public class WorkerAutoTankDip : BackgroundService
    {
        private readonly ILogger<WorkerAutoTankDip> _logger;
        private readonly TankDipConfiguration _tankConfig;
        private readonly IntervalPosting _intervalPosting;
        private readonly DBHerper _dbHelper;
        private readonly FileLogger _fileLogger;

        public WorkerAutoTankDip(ILogger<WorkerAutoTankDip> logger, TankDipConfiguration tankConfig, IntervalPosting intervalPosting, FileLogger fileLogger)
        {
            _logger = logger;
            _tankConfig = tankConfig;
            _intervalPosting = intervalPosting;
            _dbHelper = new DBHerper();
            _fileLogger = fileLogger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Log startup info
            _fileLogger.LogRaw("");
            _fileLogger.LogRaw("═══════════════════════════════════════════════════════════════════════════════");
            _fileLogger.LogRaw("  AUTO TANKDIP POSTING SERVICE STARTED");
            _fileLogger.LogRaw($"  Posting Interval : {_intervalPosting.TimeInterval}ms");
            _fileLogger.LogRaw($"  SAP URL          : {_tankConfig.UrlPosting}");
            _fileLogger.LogRaw($"  Plant Code       : {_tankConfig.PlantCode}");
            _fileLogger.LogRaw($"  Log Directory    : {_fileLogger.GetLogDirectory()}");
            _fileLogger.LogRaw("═══════════════════════════════════════════════════════════════════════════════");
            _fileLogger.LogRaw("");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UploadTankTicket();
                }
                catch (Exception ex)
                {
                    _fileLogger.LogError(ex, "Exception in main loop");
                    _logger.LogInformation("Exception at: {time}, {0}", DateTimeOffset.Now, ex.Message);
                }
                await Task.Delay(_intervalPosting.TimeInterval, stoppingToken);
            }

            _fileLogger.LogInfo("Service stopping...");
        }
        /// <summary>
        /// Upload pending tank tickets to SAP
        /// </summary>
        private async Task UploadTankTicket()
        {
            var tankTicketUnUploaded = _dbHelper.GetTankTicketNotPosted();

            if (tankTicketUnUploaded.Count == 0)
            {
                return; // No tickets to process
            }

            _fileLogger.LogInfo($"Found {tankTicketUnUploaded.Count} ticket(s) pending upload");

            foreach (var ticket in tankTicketUnUploaded)
            {
                _fileLogger.LogSeparator();
                _fileLogger.LogInfo($"[POSTING] Processing Ticket: {ticket.Ticket_Number}");
                _fileLogger.LogInfo($"  Tank: {ticket.Tank_Number} | Operation: {ticket.Operation_Type} | Status: {ticket.Operation_Status}");

                // Tank ticket dengan statusreservasi 1 (PSC) tidak diupload
                if (ticket.StatusReservasi == 1)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";
                    _fileLogger.LogInfo($"  [SKIP] StatusReservasi=1 (PSC) - Not uploaded to SAP");
                }
                // Tank ticket dengan statusreservasi 2 (Routine Dipping) tidak diupload
                else if (ticket.StatusReservasi == 2)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";
                    _fileLogger.LogInfo($"  [SKIP] StatusReservasi=2 (RD) - Not uploaded to SAP");
                }
                else
                {
                    TMS.SAP.TankDipPosting.ResponseCode _responseCode = new TMS.SAP.TankDipPosting.ResponseCode();

                    try
                    {
                        _responseCode = UploadingTicket(ticket);
                    }
                    catch (Exception ex)
                    {
                        _fileLogger.LogError(ex, $"  [ERROR] Failed to upload ticket to SAP");
                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = $"Upload failed: {ex.Message}";
                        _dbHelper.UpdateTankTicket(ticket);
                        continue; // Continue to next ticket
                    }

                    if (_responseCode.Type == "S")
                    {
                        ticket.Is_Upload_Success = true;
                        ticket.SAP_Response = _responseCode.DescMsg;
                        _fileLogger.LogInfo($"  [SUCCESS] SAP Response: {_responseCode.DescMsg}");

                        // Parse additionalQuantity dari SAP response untuk mendapatkan nilai volume
                        // Wrapped in try-catch to ensure program continues even if parsing fails
                        try
                        {
                            ParseAndUpdateVolumeFromSAPResponse(ticket, _responseCode);
                        }
                        catch (Exception parseEx)
                        {
                            _fileLogger.LogWarning($"  [VOLUME-PARSE] Failed to parse volume from SAP response: {parseEx.Message}");
                            _fileLogger.LogWarning($"  [VOLUME-PARSE] Ticket will be saved without volume data - Program continues");
                        }
                    }
                    else if (_responseCode.Type == "E")
                    {
                        if (_responseCode.DescMsg.Contains("already posted"))
                        {
                            ticket.Is_Upload_Success = true;
                            ticket.SAP_Response = _responseCode.DescMsg;
                            _fileLogger.LogInfo($"  [ALREADY-POSTED] {_responseCode.DescMsg}");
                        }
                        else
                        {
                            _fileLogger.LogWarning($"  [SAP-ERROR] {_responseCode.DescMsg}");

                            // Tetap parse additionalQuantity meskipun error (SAP kadang tetap return nilai)
                            if (_responseCode.additionalQuantity != null && _responseCode.additionalQuantity.Length > 0)
                            {
                                try
                                {
                                    ParseAndUpdateVolumeFromSAPResponse(ticket, _responseCode);
                                }
                                catch (Exception parseEx)
                                {
                                    _fileLogger.LogWarning($"  [VOLUME-PARSE] Failed to parse volume on error response: {parseEx.Message}");
                                }
                            }
                            ticket.Is_Upload_Success = false;
                            ticket.SAP_Response = _responseCode.DescMsg;
                        }
                    }
                    else
                    {
                        _fileLogger.LogWarning($"  [UNKNOWN] Unexpected SAP response type: {_responseCode.Type}");
                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = $"Unknown response type: {_responseCode.Type}";
                    }

                    _logger.LogInformation("Post response at: {time}, {0} ,{1}", DateTimeOffset.Now, _responseCode.Type, _responseCode.DescMsg);
                }

                _dbHelper.UpdateTankTicket(ticket);
                _fileLogger.LogInfo($"  [SAVED] Ticket {ticket.Ticket_Number} updated in database");
                _logger.LogInformation("Worker running at: {time}, Ticket Posting {0} : {1}", DateTimeOffset.Now, ticket.Ticket_Number, ticket.SAP_Response);

            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        private TMS.SAP.TankDipPosting.ResponseCode UploadingTicket(TankTicket ticket)
        {
            //var tank
            var tankLiveData = _dbHelper.GetTankByName(ticket.Tank_Number);
            //
            TMS.SAP.TankDipPosting.ResponseCode responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
            //read configuration
            string url = _tankConfig.UrlPosting;
            #region Initial value
            TMS.SAP.TankDipPosting.DippingDataDelivery data = new TMS.SAP.TankDipPosting.DippingDataDelivery();
            data.plant = _tankConfig.PlantCode;
            data.dipDate = ((DateTime)ticket.Timestamp).ToString("ddMMyyyy");
            data.dipTime = ((DateTime)ticket.Timestamp).ToString("HHmm");
            data.dipTank = ConvertTankNumberToSAPTankNumber(tankLiveData.Tank_Number);
            data.dipEvent = ConvertStatusToDipEvent(ticket.Operation_Status);
            data.dipOperation = ConvertStatusReservasiToDipOperation(ticket.StatusReservasi);
            data.totalHeight = ticket.LiquidLevel?.ToString() ?? "0";
            data.totalHeightUOM = "MM";
            data.waterHeight = ticket.WaterLevel?.ToString() ?? "0";
            data.waterHeightUOM = "MM";
            data.celciusMaterialTemp = ticket.LiquidTemperature?.ToString("0.00") ?? "0.00";
            data.celciusMaterialTemp = data.celciusMaterialTemp.Replace('.', ',');
            data.celciusTestTemp = ticket.LiquidTemperature?.ToString("0.00") ?? "0.00";
            data.celciusTestTemp = data.celciusTestTemp.Replace('.', ',');
            data.kglDensity = ticket.LiquidDensity?.ToString("0.0000") ?? "0.0000";
            data.kglDensity = data.kglDensity.Replace('.', ',');
            if (ticket.StatusReservasi == 4)
            {
                data.shipment = ticket.Shipment_Id;
                data.delivery = ticket.Do_Number;
            }
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
            try
            {
                response = service.DoPosting(request);
                //respon byd
                TMS.SAP.TankDipPosting.DoPostingResponseBody responseBody = response.Body;
                responseCode = responseBody.DoPostingResult;
                return responseCode;
            }
            catch(Exception ex)
            {
                throw new Exception($"Exception Posting Tankticket to MYSAP {ex.Message}");
            }
           
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tanknumber"></param>
        /// <returns></returns>
        private string ConvertTankNumberToSAPTankNumber(string tanknumber)
        {
            string[] splits = tanknumber.Split('-');
            int tanknum = Convert.ToInt32(splits[1]);

            return "T" + tanknum.ToString("000");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
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
        /// Convert StatusReservasi to SAP Operation Code
        /// </summary>
        private string ConvertStatusReservasiToDipOperation(int? statusReservasi)
        {
            if (!statusReservasi.HasValue || statusReservasi.Value == 1 || statusReservasi.Value == 2)
                return "";

            return statusReservasi.Value switch
            {
                3 => "TT",   // Tank to Tank Transfer
                4 => "ROT",  // Receiving Others
                5 => "ILS",  // Issue to LSTK/Sales
                6 => "PI",   // Physical Inventory
                7 => "CHK",  // Stock Checking
                8 => "UDG",  // Upgrade/Downgrade
                9 => "BLD",  // Blending
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
                _fileLogger.LogWarning($"  [VOLUME-PARSE] SAP response tidak memiliki additionalQuantity untuk ticket {ticket.Ticket_Number}");
                _fileLogger.LogWarning($"  [VOLUME-PARSE] Program continues without volume data");
                return;
            }

            _fileLogger.LogInfo($"  [VOLUME-PARSE] Parsing {responseCode.additionalQuantity.Length} quantity values from SAP");

            int parsedCount = 0;
            foreach (var quantity in responseCode.additionalQuantity)
            {
                if (string.IsNullOrEmpty(quantity.Uom) || string.IsNullOrEmpty(quantity.Qty))
                {
                    _fileLogger.LogWarning($"  [VOLUME-PARSE] Skipping empty UOM/Qty entry");
                    continue;
                }

                // Parse quantity value (SAP menggunakan koma sebagai decimal separator)
                string qtyString = quantity.Qty.Replace(',', '.');
                if (!double.TryParse(qtyString, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    _fileLogger.LogWarning($"  [VOLUME-PARSE] Failed to parse value '{quantity.Qty}' for UOM '{quantity.Uom}'");
                    continue;
                }

                switch (quantity.Uom.ToUpper())
                {
                    case "BB6":
                        // Barrel @ 60°F
                        ticket.VolumeBarrel60F = val;
                        _fileLogger.LogInfo($"    Volume Barrel@60F : {val:N2} BBL");
                        parsedCount++;
                        break;

                    case "L":
                        // Volume Product (Observed Volume in Liters)
                        ticket.Volume = val;
                        _fileLogger.LogInfo($"    Volume Product    : {val:N2} L");
                        parsedCount++;
                        break;

                    case "L15":
                        // Volume @ 15°C (Standard Volume)
                        ticket.Volume15C = val;
                        _fileLogger.LogInfo($"    Volume @15C       : {val:N2} L");
                        parsedCount++;
                        break;

                    case "LTO":
                        // Long Ton
                        ticket.VolumeLongTon = val;
                        _fileLogger.LogInfo($"    Volume LongTon    : {val:N4} LTO");
                        parsedCount++;
                        break;

                    case "MT":
                        // Metric Ton - tidak disimpan di model saat ini
                        _fileLogger.LogInfo($"    Metric Ton (skip) : {val:N4} MT");
                        break;

                    default:
                        _fileLogger.LogWarning($"  [VOLUME-PARSE] Unknown UOM '{quantity.Uom}' with value {val}");
                        break;
                }
            }

            if (parsedCount > 0)
            {
                _fileLogger.LogInfo($"  [VOLUME-PARSE] Successfully parsed {parsedCount} volume values for ticket {ticket.Ticket_Number}");
            }
            else
            {
                _fileLogger.LogWarning($"  [VOLUME-PARSE] No volume values could be parsed for ticket {ticket.Ticket_Number}");
            }
        }
    }
}