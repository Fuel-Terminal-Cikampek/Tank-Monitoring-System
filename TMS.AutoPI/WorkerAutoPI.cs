using EasyModbus;
using System.Text.Json;
using TMS.Models;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.IO.Ports;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.Formula.Functions;
using Microsoft.Extensions.Logging;
using TMS.SAP.TankDipPosting;

namespace TMS.AutoPI
{
    public class WorkerAutoPI : BackgroundService
    {
        private readonly ILogger<WorkerAutoPI> _logger;
        private readonly AutoPIConfiguration _autoPIConfig;
        private readonly DBHerper _dbHelper;
        private readonly IServiceScopeFactory _scopeFactory;
        private ModbusClient _modbusClient;
        public WorkerAutoPI(ILogger<WorkerAutoPI> logger, AutoPIConfiguration autoPIConfiguration, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _autoPIConfig = autoPIConfiguration;
            _dbHelper = new DBHerper();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CreatePI();
                    //await CreateCHK();
                    if (_autoPIConfig.PostingStatus)
                    {
                        await UploadTankTicket();
                    }
                    
                }
                catch(Exception ex)
                {
                    _logger.LogInformation($"Exception {ex.Message}");
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
        private async Task CreatePI()
        {
            //
            string[] arrStartTime = _autoPIConfig.TimeStart.Split(':');
            string[] arrEndTime = _autoPIConfig.TimeEnd.Split(':');
            //
            var tanks = _dbHelper.GetAllTankActive();
            
            DateTime now = DateTime.Now;


            DateTime PIdate = now.AddDays(-1);
            string month = MonthFormat(PIdate); //change format month to roman
            if (now.Hour >= Convert.ToInt32(arrStartTime[0]) && now.Hour <= Convert.ToInt32(arrEndTime[0]) && now.Minute >= Convert.ToInt32(arrStartTime[1]) && now.Minute <= Convert.ToInt32(arrEndTime[1]) && now.Second >= Convert.ToInt32(arrStartTime[2]) && now.Second <= Convert.ToInt32(arrEndTime[2]))
            {
                var checkPITankTicket = _dbHelper.CheckTankPINow(PIdate);
                foreach (var tank in tanks)
                {
                        if(tank.IsAutoPI ?? false)  // Handle nullable bool
                        {
                                var tankTicket = new TankTicket();
                                var tankRecords = _dbHelper.GetLiveDataByTankName(tank.Tank_Name);
                                var getPI = _dbHelper.GetPI();
                                string tankName = tank.Tank_Name;
                                string tankno = tankName.Substring(2);
                                //string tankticketno = "AOPI" + tankno + "/TANK-TICKET/JAMBI/" + PIdate.Day + "/" + month + "/" + now.Year + "";
                                try
                                {
                                    var checkTankTicketNow = checkPITankTicket.FirstOrDefault(t => t.Tank_Number == tank.Tank_Name);
                                    if (checkTankTicketNow ==null)
                                    {
                                        tankTicket.Tank_Number = tank.Tank_Name;
                                        tankTicket.StatusReservasi = 6; //Physical Inventory code
                                        tankTicket.Measurement_Method = "AUTO"; //automatic tank dipp
                                        tankTicket.Operation_Type = "PI";
                                        tankTicket.Ticket_Number = TicketRenderer(tankTicket.Operation_Type, getPI);
                                        tankTicket.Operation_Status = 2;
                                        tankTicket.Timestamp = PIdate.Date.AddHours(23).AddMinutes(59).AddSeconds(00);
                                        tankTicket.LiquidLevel = (int?)Math.Round((double)(tankRecords?.Level ?? 0),0);
                                        tankTicket.WaterLevel = (int?)Math.Round(tank.IsManualWaterLevel ==true?(tank.ManualWaterLevel ?? 0): (tankRecords?.Level_Water ?? 0),0);
                                        tankTicket.LiquidTemperature =Math.Round(tank.IsManualTemp == true ? (tank.ManualTemp ?? 0) : (tankRecords?.Temperature ?? 0),2);
                                        tankTicket.TestTemperature = Math.Round((tank.IsManualTestTemp == true ? (tank.ManualTestTemp ?? 0) : 0),2);
                                        tankTicket.LiquidDensity = Math.Round((tank.IsManualDensity == true ? (tank.ManualLabDensity ?? 0) : (tankRecords?.Density ?? 0)),3);
                                        // Note: Volume, Pumpable, Ullage, FlowRateKLperH properties don't exist in current TankTicket model
                                        // These need to be calculated or retrieved from TankLiveData if available
                                        // tankTicket.Volume = Math.Round(tankRecords?.Volume_Obs ?? 0,0);
                                        // tankTicket.Pumpable = 0;
                                        // tankTicket.Ullage = 0; // Calculate as: TankMaxVolume - CurrentVolume
                                        // tankTicket.FlowRateKLperH = Math.Round(tankRecords?.Flowrate ?? 0,0);
                                        tankTicket.Created_By = "AUTO";
                                        tankTicket.Created_Timestamp = now;
                                        _dbHelper.AddTankTicket(tankTicket);
                                        _logger.LogInformation("{time} Add Tank Ticket PI {0}", DateTimeOffset.Now, tank.Tank_Name);

                                    }
                                    else
                                    {
                                        _logger.LogInformation($"Tank ticket has created on start {_autoPIConfig.TimeStart} and end time {_autoPIConfig.TimeEnd} !!!");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("{time} Exception Tank Ticket PI {0} :{1}", DateTimeOffset.Now, tank.Tank_Name, ex.Message);
                                }
                        }
                    

                }
            }
        }

        private async Task CreateCHK()
        {
            //
            string filterTime1 = _autoPIConfig.TimeFilter1;
            string filterTime2 = _autoPIConfig.TimeFilter2;
            string filterTime3 = _autoPIConfig.TimeFilter3;
            string filterTime4 = _autoPIConfig.TimeFilter4;
            //
            var tanks = _dbHelper.GetAllTankActive();
            
            DateTime now = DateTime.Now;
            string nowString = now.ToString("HH:mm");

            foreach (var tank in tanks)
            {
                if (tank.IsAutoPI ?? false)  // Handle nullable bool
                {
                    var tankTicket = new TankTicket();
                    var tankRecords = _dbHelper.GetLiveDataByTankName(tank.Tank_Name);
                    
                    DateTime CHKDate = now.Date.AddHours(now.Hour).AddMinutes(now.Minute);
                    string month = MonthFormat(CHKDate); //change format month to roman
                    if (nowString == filterTime1 || nowString == filterTime2 || nowString == filterTime3 || nowString == filterTime4)
                    {
                        string tankName = tank.Tank_Name;
                        string tankno = tankName.Substring(2);
                        string tankticketno = "AOCHK" + tankno + "/TANK-TICKET/JAMBI/" + DateTime.Now.ToString("HHmmss") + DateTime.Now.ToString("/dd") + "/" + MonthFormatCHK(DateTime.Now.ToString("MM")) + DateTime.Now.ToString("/yyyy");
                        try
                        {
                            var checkTankTicketNow = _dbHelper.CheckTankCHKNow(tank.Tank_Name, CHKDate);
                            if (checkTankTicketNow.Count == 0)
                            {
                                tankTicket.Tank_Number = tank.Tank_Name;
                                tankTicket.Ticket_Number = tankticketno;
                                tankTicket.StatusReservasi = 6; //Physical Inventory code
                                tankTicket.Measurement_Method = "AUTO"; //automatic tank dipp
                                tankTicket.Operation_Type = "CHK";
                                tankTicket.Operation_Status = 2;
                                tankTicket.Timestamp = CHKDate;
                                tankTicket.LiquidLevel = tankRecords?.Level;
                                tankTicket.WaterLevel = tankRecords?.Level_Water;
                                tankTicket.LiquidTemperature = tankRecords?.Temperature ?? 0;
                                tankTicket.LiquidDensity = tankRecords?.Density ?? 0;
                                tankTicket.TestTemperature = tank.ManualTestTemp ?? 0;
                                // Note: Volume, Pumpable, Ullage, FlowRateKLperH properties don't exist in current TankTicket model
                                // tankTicket.Volume = tankRecords?.Volume_Obs ?? 0;
                                // tankTicket.Pumpable = 0;
                                // tankTicket.Ullage = 0;
                                // tankTicket.FlowRateKLperH = tankRecords?.Flowrate ?? 0;
                                tankTicket.Created_By = "AUTO";
                                tankTicket.Created_Timestamp = now;
                                _dbHelper.AddTankTicket(tankTicket);
                                _logger.LogInformation("{time} Add Tank Ticket CHK {0}", DateTimeOffset.Now, tank.Tank_Name);

                            }
                            else
                            {
                                _logger.LogInformation($"Tank ticket has created!!!");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("{time} Exception Tank Ticket CHK {0} :{1}", DateTimeOffset.Now, tank.Tank_Name, ex.Message);
                        }
                    }


                }
            }
        }

        private string MonthFormat(DateTime timestamps)
        {
            String month = timestamps.ToString("MM");
            String monthformated = null;
            if (month == "01")
                monthformated = "I";
            else if (month == "02")
                monthformated = "II";
            else if (month == "03")
                monthformated = "III";
            else if (month == "04")
                monthformated = "IV";
            else if (month == "05")
                monthformated = "V";
            else if (month == "06")
                monthformated = "VI";
            else if (month == "07")
                monthformated = "VII";
            else if (month == "08")
                monthformated = "VIII";
            else if (month == "09")
                monthformated = "IX";
            else if (month == "10")
                monthformated = "X";
            else if (month == "11")
                monthformated = "XI";
            else if (month == "12")
                monthformated = "XII";
            return monthformated;
        }

        private string MonthFormatCHK(string month)
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


        private string TicketRenderer(string type, List<TankTicket> tickets)
        {
            string[] validTypes = { "BLD", "DL", "ICR", "ILS", "IOT", "IS", "IST", "OWN", "PI", "RCR", "ROT", "RPR", "TPL", "TRF", "TT", "UDG", "CHK" };
            if (!validTypes.Contains(type))
            {
                return null;
            }

            var latestTicket = tickets.LastOrDefault();

            DateTime now = DateTime.Now;
            int currentYear = now.Year;
            int currentMonth = now.Month;

            int countTicket = _dbHelper.CountTankPI(type);

            countTicket += 1;

            string ticket = $"{type}/{currentYear:0000}{currentMonth:00}{countTicket:0000}";

            var lastTicketNumberStr = latestTicket.Ticket_Number.Replace($"{type}/", "");
            var ticketNumberStr = ticket.Replace($"{type}/", "");

            int lastTicketNumber = int.Parse(lastTicketNumberStr);
            int ticketNumber = int.Parse(ticketNumberStr);

            if (lastTicketNumber >= ticketNumber)
            {
                lastTicketNumber += 1;
                ticket = $"{type}/{lastTicketNumber}";
            }

            return ticket;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task UploadTankTicket()
        {
            var ticketPriority = _dbHelper.getTicketPriority();

            foreach (var ticket in ticketPriority)
            {

                //tank ticket dengan statusreservasi 1 tidak diupload
                if (ticket.StatusReservasi == 1)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";

                }
                //tank ticket dengan statusreservasi 2 tidak diupload
                if (ticket.StatusReservasi == 2)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";
                }
                else
                {
                    try
                    {
                        TMS.SAP.TankDipPosting.ResponseCode _responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
                        _responseCode = UploadingTicket(ticket);
                        if (_responseCode.Type == "S")
                        {
                            ticket.Is_Upload_Success = true;
                            ticket.SAP_Response = _responseCode.DescMsg;
                        }
                        else if (_responseCode.Type == "E")
                        {
                            if (_responseCode.DescMsg.Contains("already posted"))
                            {
                                ticket.Is_Upload_Success = true;
                                ticket.SAP_Response = _responseCode.DescMsg;
                            }
                            else
                            {
                                ticket.Is_Upload_Success = false;
                                ticket.SAP_Response = _responseCode.DescMsg;
                            }
                        }

                        _logger.LogInformation("Post response at: {time}, {0} ,{1}", DateTimeOffset.Now, _responseCode.Type, _responseCode.DescMsg);
                    }
                    catch (Exception ex)
                    {
                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = "Failed Posting Tankticket to MYSAP";
                        _logger.LogError("Error response at: {time}, {0}", DateTimeOffset.Now, ex.Message);
                        await Task.Delay(1000);
                    }
                    _dbHelper.UpdateTankTicket(ticket);
                }

                _logger.LogInformation("Worker running at: {time}, Ticket Posting {0} : {1}", DateTimeOffset.Now, ticket.Ticket_Number, ticket.SAP_Response);

            }


            var tankTicketUnUploaded = _dbHelper.tankTicketPIUnUploaded();

            foreach (var ticket in tankTicketUnUploaded)
            {

                //tank ticket dengan statusreservasi 1 tidak diupload
                if (ticket.StatusReservasi == 1)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";

                }
                //tank ticket dengan statusreservasi 2 tidak diupload
                if (ticket.StatusReservasi == 2)
                {
                    ticket.Is_Upload_Success = true;
                    ticket.SAP_Response = "This Tank Ticket Type is not uploaded to MySAP";
                }
                else
                {
                    try
                    {
                        TMS.SAP.TankDipPosting.ResponseCode _responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
                        _responseCode = UploadingTicket(ticket);
                        if (_responseCode.Type == "S")
                        {
                            ticket.Is_Upload_Success = true;
                            ticket.SAP_Response = _responseCode.DescMsg;
                        }
                        else if (_responseCode.Type == "E")
                        {
                            if (_responseCode.DescMsg.Contains("already posted"))
                            {
                                ticket.Is_Upload_Success = true;
                                ticket.SAP_Response = _responseCode.DescMsg;
                            }
                            else
                            {
                                ticket.Is_Upload_Success = false;
                                ticket.SAP_Response = _responseCode.DescMsg;
                            }
                        }
                       
                        _logger.LogInformation("Post response at: {time}, {0} ,{1}", DateTimeOffset.Now, _responseCode.Type, _responseCode.DescMsg);
                    }
                    catch (Exception ex)
                    {
                        ticket.Is_Upload_Success = false;
                        ticket.SAP_Response = "Failed Posting Tankticket to MYSAP";
                        _logger.LogError("Error response at: {time}, {0}", DateTimeOffset.Now, ex.Message);
                        await Task.Delay(1000);
                    }
                    _dbHelper.UpdateTankTicket(ticket);
                }

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
                var tank = _dbHelper.GetTankByName(ticket.Tank_Number);
                //
                TMS.SAP.TankDipPosting.ResponseCode responseCode = new TMS.SAP.TankDipPosting.ResponseCode();
                //read configuration
                string url = _autoPIConfig.UrlPosting;
                #region Initial value
                TMS.SAP.TankDipPosting.DippingDataDelivery data = new TMS.SAP.TankDipPosting.DippingDataDelivery();
                data.plant = _autoPIConfig.PlantCode;
                data.dipDate = ((DateTime)ticket.Timestamp).ToString("ddMMyyyy");
                data.dipTime = ((DateTime)ticket.Timestamp).ToString("HHmm");
                data.dipTank = ConvertTankNumberToSAPTankNumber(tank.Tank_Name);
                data.dipEvent = ConvertStatusToDipEvent(ticket.Operation_Status);
                data.dipOperation = ticket.Operation_Type;
                data.totalHeight = ticket.LiquidLevel.ToString();
                data.totalHeightUOM = "MM";
                data.waterHeight = ticket.WaterLevel.ToString();
                data.waterHeightUOM = "MM";
                data.celciusMaterialTemp = ticket.LiquidTemperature == 0 ? "" : ticket.LiquidTemperature?.ToString("0.00") ?? "0.00";
                data.celciusMaterialTemp = data.celciusMaterialTemp.Replace('.', ',');
                data.celciusTestTemp = ticket.TestTemperature == 0 ? "" : ticket.TestTemperature.ToString("0.00");
                data.celciusTestTemp = data.celciusTestTemp.Replace('.', ',');
                data.kglDensity = ticket.LiquidDensity == 0 ? "" : ticket.LiquidDensity?.ToString("0.0000") ?? "0.0000";
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
                try
                {
                    _logger.LogInformation("Try to Post Tank ticket ID {0} at: {time}", DateTimeOffset.Now,ticket.Ticket_Number);
                    response = service.DoPosting(request);
                    //respon byd
                    TMS.SAP.TankDipPosting.DoPostingResponseBody responseBody = response.Body;
                    responseCode = responseBody.DoPostingResult;
                    return responseCode;
                }
                catch (Exception ex)
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
        }
}