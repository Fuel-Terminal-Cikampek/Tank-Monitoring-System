using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;
using TMS.Settings;

namespace TMS.Service
{
    public class DBHerper
    {
        private AppDbContext _Context = null!;

        private DbContextOptions<AppDbContext> GetAllOptions()
        {
            var optionBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionBuilder.UseSqlServer(AppSetting.ConnectionString);
            return optionBuilder.Options;
        }

        private List<Tank> GetTanks()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tanks = _Context.Tank.ToList();
                    return tanks;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get Tanks {ex.Message}");
                }
            }
        }

        private TankLiveData? GetLiveDataByTankName(string tankName)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var livedata = _Context.Tank_Live_Data.FirstOrDefault(t => t.Tank_Number == tankName);
                    return livedata;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get LiveData {ex.Message}");
                }
            }
        }

        private void AddHistorical(TankHistorical tankHistorical)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    _Context.Add(tankHistorical);
                    _Context.SaveChanges();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Update Tank {ex.Message}");
                }
            }
        }

        public ErrorMessage SetHistorycalData()
        {
            ErrorMessage errorMessage = new ErrorMessage();
            try
            {
                var tanks = GetTanks();
                if (tanks != null)
                {
                    foreach (var tank in tanks)
                    {
                        var tlData = GetLiveDataByTankName(tank.Tank_Name);
                        
                        // ✅ FIX: Use correct property names from TankHistorical model
                        TankHistorical tankHistorical = new TankHistorical
                        {
                            Tank_Number = tank.Tank_Name,
                            TimeStamp = DateTime.Now,
                            Level = tlData?.Level,
                            Level_Water = tank.IsManualWaterLevel == true 
                                ? (int?)(tank.ManualWaterLevel ?? 0) 
                                : tlData?.Level_Water,
                            Temperature = tank.IsManualTemp == true 
                                ? (tank.ManualTemp ?? 0) 
                                : tlData?.Temperature,
                            Density = tank.IsManualDensity == true 
                                ? (tank.ManualLabDensity ?? 0) 
                                : tlData?.Density,
                            Volume_Obs = tlData?.Volume_Obs,
                            Density_Std = tlData?.Density_Std,
                            Volume_Std = tlData?.Volume_Std,
                            Volume_LongTons = tlData?.Volume_LongTons,
                            Volume_BBL60F = tlData?.Volume_BBL60F,
                            Flowrate = tlData?.Flowrate ?? 0,
                            Pumpable = tlData?.Pumpable,
                            Ullage = tlData?.Ullage ?? 0,
                            Alarm_Status = tlData?.Alarm_Status,
                            AlarmMessage = tlData?.AlarmMessage
                        };
                        
                        AddHistorical(tankHistorical);
                    }

                    errorMessage.Status = "Success";
                    errorMessage.Message = "--";
                }
            }
            catch (Exception e)
            {
                errorMessage.Status = "Error";
                errorMessage.Message = e.Message;
            }
            return errorMessage;
        }
    }

    public class ErrorMessage
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
