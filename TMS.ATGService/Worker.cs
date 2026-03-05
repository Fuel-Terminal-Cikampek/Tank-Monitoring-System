using EasyModbus;
using System.Text.Json;
using TMS.Models;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.Diagnostics;
using NPOI.SS.Formula.Functions;

namespace TMS.ATGService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly tankConfiguration _tankConfig;
        private readonly DBHerper _dbHelper;
        private ModbusClient _modbusClient;
        private double[] lastVolume;
        private int cnt = 0;
        //tank table
        private List<TankTable> _tankTables;
        private List<Tank> _tanks;
        public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory, tankConfiguration tankConfig)
        {
            _logger = logger;
            _tankConfig = tankConfig;
            _modbusClient = new ModbusClient();
            _dbHelper = new DBHerper(loggerFactory.CreateLogger<DBHerper>());
            _tankTables = new List<TankTable>();
            GetTankTable();
            _modbusClient.IPAddress = _tankConfig.tankIp;
            _modbusClient.Port = _tankConfig.tankPort;
            lastVolume = new double[_tankConfig.tankDetail.Count()];
            _tanks = _dbHelper.GetTanks();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await UpdateData();
                await Task.Delay(_tankConfig.timeInterval, stoppingToken);
            }
        }
        //get data tank tank
        private async Task UpdateData()
        {
           
            try
            {
                _modbusClient.Connect();
                int[] allData = new int[_tankConfig.LengthRegister];
                if(allData.Length <= 100) 
                {
                    allData = _modbusClient.ReadHoldingRegisters(0, _tankConfig.LengthRegister);
                }
                else if(allData.Length >100 &&  allData.Length <= 200)
                {
                    int lengthLast = allData.Length - 125;
                    int[] dataA = _modbusClient.ReadHoldingRegisters(0, 100);
                    int[] dataB = _modbusClient.ReadHoldingRegisters(100,100 );
                    // Salin dataA ke array baru mulai dari indeks 0
                    Array.Copy(dataA, 0, allData, 0, dataA.Length);

                    // Salin dataB ke array baru mulai dari indeks setelah dataA
                    Array.Copy(dataB, 0, allData, dataA.Length, dataB.Length);
                }
                else if(allData.Length >200 && allData.Length <= 300)
                {
                    int[] dataA = _modbusClient.ReadHoldingRegisters(0, 100);
                    int[] dataB = _modbusClient.ReadHoldingRegisters(100, 100);
                    int[] dataC = _modbusClient.ReadHoldingRegisters(200, 25);
                    // Salin dataA ke array baru mulai dari indeks 0
                    Array.Copy(dataA, 0, allData, 0, dataA.Length);

                    // Salin dataB ke array baru mulai dari indeks setelah dataA
                    Array.Copy(dataB, 0, allData, 100, dataB.Length);
                    Array.Copy(dataC, 0, allData, 200, dataC.Length);
                }
                
                foreach (TankDetail td in _tankConfig.tankDetail)
                {
                    try
                    {
                        //_modbusClient.Connect();
                        //int[] data = _modbusClient.ReadHoldingRegisters(td.startAddr, td.stopAddr);
                        int[] data = allData.Skip(td.startAddr).Take(td.stopAddr).ToArray();
                        #region update tank Live Data
                        TankLiveData tankLiveData = new TankLiveData();
                        tankLiveData = _dbHelper.GetTankLiveData(td.TankId);
                        if (tankLiveData != null)
                        {
                            var status = ConvertToFloat(data[0], data[1]);
                            if (status == 0)
                            {
                                var tankDeadstock = _dbHelper.GetTankDeadStock(td.TankId);
                                var maxVolume = _dbHelper.GetMaxVolume(td.TankId);
                                tankLiveData.Status = "NORMAL";
                                tankLiveData.LiquidLevel = ConvertToFloat(data[2], data[3]);
                                tankLiveData.WaterLevel = ConvertToFloat(data[4], data[5]);
                                tankLiveData.LiquidTemperature = ConvertToFloat(data[6], data[7]) / 10; //get temperature
                                tankLiveData.LiquidDensity = ConvertToFloat(data[8], data[9]) / 1000; //get density
                                tankLiveData.Volume = GetVolume(td.TankName, (double)tankLiveData.LiquidLevel);
                                tankLiveData.Ullage = maxVolume - tankLiveData.Volume;
                                tankLiveData.Pumpable = tankLiveData.Volume - tankDeadstock;
                                tankLiveData.TimeStamp = DateTime.Now;
                                //ALARM MANAGEMENT
                                var tank = _dbHelper.GetTanks().FirstOrDefault(t=>t.TankId ==  tankLiveData.TankId);
                                if (tankLiveData.LiquidLevel >= tank.LevelHH)
                                {
                                    if ((bool)tank.UseAlarmHH &&(bool)tankLiveData.Ack)
                                    {
                                        tankLiveData.AlarmStatus = "HH";
                                        tankLiveData.AlarmMessage = string.Format("Alarm status HH On {0} !!!", tank.Tank_Name);
                                        //tankLiveData.Ack = true;
                                    }
                                   
                                }
                                else if (tankLiveData.LiquidLevel >= tank.LevelH && tankLiveData.LiquidLevel < tank.LevelHH)
                                {
                                    if ((bool)tank.UseAlarmH && (bool)tankLiveData.Ack)
                                    {
                                        tankLiveData.AlarmStatus = "H";
                                        tankLiveData.AlarmMessage = string.Format("Alarm status H On {0} !!!", tank.Tank_Name);
                                        //tankLiveData.Ack = true;
                                    }
                                        
                                }

                                else if (tankLiveData.LiquidLevel <= tank.LevelLL)
                                {
                                    if ((bool)tank.UseAlarmLL && (bool)tankLiveData.Ack)
                                    {
                                        tankLiveData.AlarmStatus = "LL";
                                        tankLiveData.AlarmMessage = string.Format("Alarm status LL On {0} !!!", tank.Tank_Name);
                                        //tankLiveData.Ack = true;
                                    }
                                }
                                else if (tankLiveData.LiquidLevel <= tank.LevelL && tankLiveData.LiquidLevel > tank.LevelLL)
                                {
                                    if ((bool)tank.UseAlarmL && (bool)tankLiveData.Ack)
                                    {
                                        tankLiveData.AlarmStatus = "L";
                                        tankLiveData.AlarmMessage = string.Format("Alarm status L On {0} !!!", tank.Tank_Name);
                                        //tankLiveData.Ack = true;
                                    }

                                }
                                else
                                {
                                    tankLiveData.AlarmStatus = null;
                                    tankLiveData.AlarmMessage = null;
                                    tankLiveData.Ack = true;
                                }
                                

                                //flowrate calculation
                                // NOTE: Perhitungan flowrate hanya dilakukan setelah cnt > cntFlowrate untuk stabilitas data
                                // TAPI update LastVolume, LastLiquidLevel, LastTimeStamp dilakukan SETIAP cycle
                                if (cnt > _tankConfig.cntFlowrate)
                                {
                                    // Hanya hitung flowrate jika LastTimeStamp sudah terisi (bukan cycle pertama)
                                    if (tankLiveData.LastTimeStamp != null && tankLiveData.LastTimeStamp.HasValue)
                                    {
                                        DateTime Timestamp = Convert.ToDateTime(tankLiveData.TimeStamp);
                                        DateTime lastTimestamp = Convert.ToDateTime(tankLiveData.LastTimeStamp);
                                        var timeTotalSecond = Timestamp.Subtract(lastTimestamp).TotalSeconds;
                                        tankLiveData.TotalSecond = (int)timeTotalSecond;

                                        // Hitung FlowRate mm/s jika ada perubahan level
                                        if ((tankLiveData.LiquidLevel - (tankLiveData.LastLiquidLevel ?? 0)) != 0 && (tankLiveData.LastLiquidLevel ?? 0) > 0)
                                        {
                                            tankLiveData.FlowRateMperSecond = GetMiliMeterPerSecond(tankLiveData.LastLiquidLevel ?? 0, (double)tankLiveData.LiquidLevel, (int)tankLiveData.TotalSecond);
                                        }
                                        else
                                        {
                                            tankLiveData.FlowRateMperSecond = 0;
                                        }

                                        // Hitung FlowRate KL/h jika LastVolume sudah terisi dan ada perubahan
                                        if ((tankLiveData.LastVolume ?? 0) > 0 && tankLiveData.TotalSecond > 0)
                                        {
                                            var flowrateKLperHour = GetLitersPerSecondToKilolitersPerHour(tankLiveData.LastVolume ?? 0, tankLiveData.Volume, (int)tankLiveData.TotalSecond);
                                            tankLiveData.FlowRateKLperH = flowrateKLperHour;
                                        }
                                        else
                                        {
                                            tankLiveData.FlowRateKLperH = 0;
                                        }
                                    }
                                    else
                                    {
                                        // Cycle pertama setelah cnt > cntFlowrate, flowrate = 0
                                        tankLiveData.FlowRateMperSecond = 0;
                                        tankLiveData.FlowRateKLperH = 0;
                                        tankLiveData.TotalSecond = 0;
                                    }
                                }
                                else
                                {
                                    // Sebelum cnt > cntFlowrate, flowrate = 0 (masa warming up)
                                    tankLiveData.FlowRateMperSecond = 0;
                                    tankLiveData.FlowRateKLperH = 0;
                                    tankLiveData.TotalSecond = 0;
                                }

                                // ✅ PENTING: Update Last values SETIAP cycle (bukan hanya saat cnt > cntFlowrate)
                                // Ini memastikan LastVolume, LastLiquidLevel, LastTimeStamp selalu terisi di database
                                tankLiveData.LastLiquidLevel = (double)tankLiveData.LiquidLevel;
                                tankLiveData.LastVolume = tankLiveData.Volume;
                                tankLiveData.LastTimeStamp = tankLiveData.TimeStamp;
                            }
                            else
                            {
                                tankLiveData.Status = "ALARM";
                                tankLiveData.LiquidLevel = 0;
                                tankLiveData.Volume = 0;
                                tankLiveData.LiquidDensity = 0;
                                tankLiveData.LiquidTemperature = 0;
                                tankLiveData.LastLiquidLevel = 0;
                                tankLiveData.LastVolume = 0;
                                tankLiveData.FlowRateKLperH = 0;
                                tankLiveData.FlowRateMperSecond = 0;
                                tankLiveData.TotalSecond = 0;
                                tankLiveData.AlarmStatus = "DISCONNECTED";
                                
                            }

                            _dbHelper.updateTnkLiveData(tankLiveData);
                            
                        }

                        #endregion
                        var json = JsonSerializer.Serialize(tankLiveData);
                        _logger.LogInformation("Worker running at: {time},ATG Is Connected on TANK ={0}: list data = {1}", DateTimeOffset.Now, td.TankName, json);
                        
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation("Exception at: {time}, {0}", DateTimeOffset.Now, e.Message);
                    }

                }
               
                _modbusClient.Disconnect();
            }
            catch(Exception ex)
            {
                _logger.LogInformation("Exception at: {time}, {0}", DateTimeOffset.Now, ex.Message);
            }
            if (cnt > _tankConfig.cntFlowrate)
            {
                cnt = 0;
            }
            cnt += 1;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastVolume"></param>
        /// <param name="Volume"></param>
        /// <returns></returns>
        static double GetLitersPerSecondToKilolitersPerHour(double LastVolume, double Volume, int totalSecond)
        {
            var volume = Math.Round(Volume, 0);
            var lastVolume = Math.Round(LastVolume, 0);
            if (totalSecond < 0)
            {
                totalSecond = totalSecond * -1;
            }
            var litersPerSecond = volume - lastVolume;
            if(litersPerSecond == 0)
            {
                return 0;
            }
            litersPerSecond = litersPerSecond/ totalSecond;
            // Conversion factors
            double litersInKiloliter = 1000.0; // 1 kL = 1000 L
            double secondsInHour = 3600.0;     // 1 hour = 3600 seconds

            // Perform the conversion
            double kilolitersPerHour = (litersPerSecond * secondsInHour) / litersInKiloliter;

            return kilolitersPerHour;
        }
        static double GetMiliMeterPerSecond(double LastLevel, double PresetLevel, int totalSecond)
        {
            var presetLevel = Math.Round(PresetLevel, 0);
            var lastLevel = Math.Round(LastLevel, 0);
            if (totalSecond < 0)
            {
                totalSecond = totalSecond * -1;
            }
            return (presetLevel - lastLevel)/totalSecond;
        }
        //Convert to float
        private float ConvertToFloat(int Array1, int Array2)
        {
            int[] newArray = new int[2];
            newArray[0] = Array1;
            newArray[1] = Array2;
            return ModbusClient.ConvertRegistersToFloat(newArray, ModbusClient.RegisterOrder.HighLow);
        }
        //get tank table
        private void GetTankTable()
        {
            foreach (TankDetail td in _tankConfig.tankDetail)
            {
                try
                {
                    //get doc CSV;
                    IWorkbook bookfile;
                    string path = string.Format("TankTable/{0}.xlsx", td.TankName);
                    using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        bookfile = new XSSFWorkbook(file);
                    }
                    ISheet sheet = bookfile.GetSheet(td.TankName);
                    //get data tank table
                    TankTable tankTable = new TankTable();
                    tankTable.TankName = td.TankName;
                    tankTable.TankTableDetails = new List<TankTableDetail>();
                    for (int row = 1; row < sheet.LastRowNum; row++)
                    {
                        TankTableDetail ttDetail = new TankTableDetail();
                        if (sheet.GetRow(row) != null)
                        {
                            if (tankTable.TankName == "T-13" || tankTable.TankName == "T-14"|| tankTable.TankName == "T-15"|| tankTable.TankName == "T-16")
                            {
                                string data1 = sheet.GetRow(row).GetCell(0).ToString();
                                string data2 = sheet.GetRow(row).GetCell(1).ToString();
                                ttDetail.Level = Convert.ToDouble(data1);
                                ttDetail.Volume = Convert.ToDouble(data2);
                                tankTable.TankTableDetails.Add(ttDetail);
                            }
                            else
                            {
                                string data = sheet.GetRow(row).GetCell(0).ToString();
                                string[] splitData = data.Split(";");
                                ttDetail.Level = Convert.ToDouble(splitData[0]);
                                ttDetail.Volume = Convert.ToDouble(splitData[1]);
                                tankTable.TankTableDetails.Add(ttDetail);
                            }
                           
                            //_logger.LogInformation("read data at: {time}, {0},{1}", DateTimeOffset.Now, ttDetail.Level, ttDetail.Volume);
                        }
                    }
                    _tankTables.Add(tankTable);
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Error Get Table at: {time}, {0}", DateTimeOffset.Now, e.Message);
                }

            }
        }
            //GET VOLUME
        private double GetVolume(string TankName, double Level)
        {
            // UpdateData();
            var level = Math.Round(Level, 0);
            double resultVolume=0;
            double lowerLevelLimit = 0; //x1
            double lowerVolumeLimit = 0; //y1

            double upperLevelLimit = 0; //x2
            double upperVolumeLimit = 0; //y2
            TankTable tb = _tankTables.Where(t => t.TankName == TankName).First();
            for(int col=0; col < tb.TankTableDetails.Count; col++)
            {
                if(level == tb.TankTableDetails[col].Level)
                {
                    resultVolume = tb.TankTableDetails[col].Volume;
                    return resultVolume;
                }
                if(tb.TankTableDetails[col].Level < level)
                {
                    lowerLevelLimit = tb.TankTableDetails[col].Level;
                    lowerVolumeLimit = tb.TankTableDetails[col].Volume;
                }
                else if(tb.TankTableDetails[col].Level > level)
                {
                    upperLevelLimit = tb.TankTableDetails[col].Level;
                    upperVolumeLimit = tb.TankTableDetails[col].Volume;
                    break;
                }               
            }
            //calculate interpolation
            resultVolume = lowerVolumeLimit + ((level - lowerLevelLimit) / (upperLevelLimit - lowerLevelLimit)) * (upperVolumeLimit - lowerVolumeLimit);

            return resultVolume;
        }
        
    }
}