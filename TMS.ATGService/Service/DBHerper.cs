using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TMS.Models;

namespace TMS.ATGService
{
    public class DBHerper
    {
        private AppDbContext _Context;
        private readonly ILogger<DBHerper> _logger;

        public DBHerper(ILogger<DBHerper> logger)
        {
            _logger = logger;
        }

        private DbContextOptions<AppDbContext> GetAllOptions()
        {
            var optionBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionBuilder.UseSqlServer(AppSetting.ConnectionString);
            return optionBuilder.Options;
        }
        public List<TankLiveData> GetAllTankLiveData()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankLiveDatas = _Context.Tank_Live_Data.ToList();
                    if (tankLiveDatas != null)
                    {
                        return tankLiveDatas;
                    }
                    else
                    {
                        return new List<TankLiveData>();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        //get tanklive data
        public TankLiveData GetTankLiveData(int id)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                   TankLiveData tankLiveData = _Context.Tank_Live_Data.Single(x => x.TankId==id);
                    if (tankLiveData != null)
                    {
                        return tankLiveData;
                    }
                    else
                    {
                        return new TankLiveData();
                    }

                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        //update tanklivedata
        public void updateTnkLiveData(TankLiveData tankLiveData)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    if (tankLiveData != null && !string.IsNullOrEmpty(tankLiveData.Tank_Number))
                    {
                        // ✅ FIX: Load existing entity WITH TRACKING to avoid EF conflicts
                        var existingEntity = _Context.Tank_Live_Data
                            .FirstOrDefault(t => t.Tank_Number == tankLiveData.Tank_Number);

                        // ✅ DEBUG: Log untuk tracking
                        Console.WriteLine($"[DB] Tank={tankLiveData.Tank_Number}, Level={tankLiveData.Level}mm, Existing={existingEntity?.Level}mm");

                        if (existingEntity != null)
                        {
                            // ✅ FIX: Update existing entity langsung (tidak pakai Attach)
                            // Ini menghindari EF tracking conflict
                            existingEntity.Product_ID = tankLiveData.Product_ID;
                            existingEntity.TimeStamp = tankLiveData.TimeStamp;
                            existingEntity.Level = tankLiveData.Level;
                            existingEntity.Level_Water = tankLiveData.Level_Water;
                            existingEntity.Temperature = tankLiveData.Temperature;
                            existingEntity.Density = tankLiveData.Density;
                            existingEntity.Volume_Obs = tankLiveData.Volume_Obs;
                            existingEntity.Density_Std = tankLiveData.Density_Std;
                            existingEntity.Volume_Std = tankLiveData.Volume_Std;
                            existingEntity.Volume_LongTons = tankLiveData.Volume_LongTons;
                            existingEntity.Volume_BBL60F = tankLiveData.Volume_BBL60F;
                            existingEntity.Flowrate = tankLiveData.Flowrate;
                            existingEntity.Alarm_Status = tankLiveData.Alarm_Status;
                            existingEntity.Level_DeadStock = tankLiveData.Level_DeadStock;
                            existingEntity.Volume_DeadStock = tankLiveData.Volume_DeadStock;
                            existingEntity.Level_Safe_Capacity = tankLiveData.Level_Safe_Capacity;
                            existingEntity.Volume_Safe_Capacity = tankLiveData.Volume_Safe_Capacity;
                            existingEntity.Ack = tankLiveData.Ack;
                            existingEntity.FlowRateMperSecond = tankLiveData.FlowRateMperSecond;
                            existingEntity.LastLiquidLevel = tankLiveData.LastLiquidLevel;
                            existingEntity.LastTimeStamp = tankLiveData.LastTimeStamp;
                            existingEntity.TotalSecond = tankLiveData.TotalSecond;
                            existingEntity.LastVolume = tankLiveData.LastVolume;
                            existingEntity.Pumpable = tankLiveData.Pumpable;
                            existingEntity.AlarmMessage = tankLiveData.AlarmMessage;
                            existingEntity.Ullage = tankLiveData.Ullage;

                            _Context.SaveChanges();
                            Console.WriteLine($"[DB] ✅ Updated Tank={tankLiveData.Tank_Number}, Level={tankLiveData.Level}mm");
                        }
                        else
                        {
                            // Insert new
                            _Context.Tank_Live_Data.Add(tankLiveData);
                            _Context.SaveChanges();
                            Console.WriteLine($"[DB] ✅ Inserted NEW Tank={tankLiveData.Tank_Number}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DB] ⚠️ SKIP: tankLiveData is null or Tank_Number is empty!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERROR updating Tank_LiveData ({tankLiveData?.Tank_Number}): {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// ✅ NEW METHOD: Update Tank_LiveDataTMS (mirror/backup data)
        /// CLEAN version - hanya 19 kolom (tanpa legacy columns)
        /// Parallel transaction - jika gagal, tidak block Tank_LiveData update
        /// </summary>
        public void updateTankLiveDataTMS(TankLiveData tankLiveData)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    if (tankLiveData != null && !string.IsNullOrEmpty(tankLiveData.Tank_Number))
                    {
                        // Convert TankLiveData to TankLiveDataTMS (all 27 columns: 18 clean + 9 legacy)
                        var tms = TankLiveDataTMS.FromTankLiveData(tankLiveData);

                        // Load existing entity WITH TRACKING to avoid EF conflicts
                        var existingEntity = _Context.Tank_LiveDataTMS
                            .FirstOrDefault(t => t.Tank_Number == tms.Tank_Number);

                        Console.WriteLine($"[DB-TMS] Tank={tms.Tank_Number}, Level={tms.Level}mm, Existing={existingEntity?.Level}mm");

                        if (existingEntity != null)
                        {
                            // ✅ UPDATE existing entity (field by field - all 27 columns)
                            // 18 Clean columns
                            existingEntity.Product_ID = tms.Product_ID;
                            existingEntity.TimeStamp = tms.TimeStamp;
                            existingEntity.Level = tms.Level;
                            existingEntity.Level_Water = tms.Level_Water;
                            existingEntity.Temperature = tms.Temperature;
                            existingEntity.Density = tms.Density;
                            existingEntity.Volume_Obs = tms.Volume_Obs;
                            existingEntity.Density_Std = tms.Density_Std;
                            existingEntity.Volume_Std = tms.Volume_Std;
                            existingEntity.Volume_LongTons = tms.Volume_LongTons;
                            existingEntity.Volume_BBL60F = tms.Volume_BBL60F;
                            existingEntity.Flowrate = tms.Flowrate;
                            existingEntity.Alarm_Status = tms.Alarm_Status;
                            existingEntity.Level_DeadStock = tms.Level_DeadStock;
                            existingEntity.Volume_DeadStock = tms.Volume_DeadStock;
                            existingEntity.Level_Safe_Capacity = tms.Level_Safe_Capacity;
                            existingEntity.Volume_Safe_Capacity = tms.Volume_Safe_Capacity;

                            // 9 Legacy columns
                            existingEntity.Ack = tms.Ack;
                            existingEntity.FlowRateMperSecond = tms.FlowRateMperSecond;
                            existingEntity.LastLiquidLevel = tms.LastLiquidLevel;
                            existingEntity.LastTimeStamp = tms.LastTimeStamp;
                            existingEntity.TotalSecond = tms.TotalSecond;
                            existingEntity.LastVolume = tms.LastVolume;
                            existingEntity.Pumpable = tms.Pumpable;
                            existingEntity.AlarmMessage = tms.AlarmMessage;
                            existingEntity.Ullage = tms.Ullage;

                            _Context.SaveChanges();
                            Console.WriteLine($"[DB-TMS] ✅ Updated Tank={tms.Tank_Number}, Level={tms.Level}mm");
                        }
                        else
                        {
                            // ✅ INSERT new record
                            _Context.Tank_LiveDataTMS.Add(tms);
                            _Context.SaveChanges();
                            Console.WriteLine($"[DB-TMS] ✅ Inserted NEW Tank={tms.Tank_Number}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DB-TMS] ⚠️ SKIP: tankLiveData is null or Tank_Number is empty!");
                    }
                }
                catch (Exception ex)
                {
                    // ⚠️ NON-BLOCKING: Log error but don't throw
                    // Tank_LiveDataTMS is backup/mirror - jika gagal, tidak boleh block Tank_LiveData
                    Console.WriteLine($"⚠️ WARNING: Failed to update Tank_LiveDataTMS ({tankLiveData?.Tank_Number}): {ex.Message}");
                    // DON'T throw - allow main Tank_LiveData update to succeed
                }
            }
        }

        //get products
        public List<Product> getProduct()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var products = _Context.Master_Product.ToList();
                    if (products != null)
                    {
                        return products;
                    }
                    else
                    {
                        return new List<Product>();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        //get products
        public List<Tank> GetTanks()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tanks = _Context.Tank.ToList();
                    if (tanks != null)
                    {
                        return tanks;
                    }
                    else
                    {
                        return new List<Tank>();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        //get tank by Name
        public Tank getTank(string Name)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {

                    var tanks = _Context.Tank.ToList();
                    var tank = tanks.FirstOrDefault(t => t.Tank_Name == Name);
                    if(tank != null)
                    {
                        return tank;
                    }
                    else
                    {
                        return new Tank();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        public void UpdateTank(Tank tank)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    _Context.Update(tank);
                    _Context.SaveChangesAsync();
                }
                catch (Exception)
                {
                    throw;
                }

            }

        }

        //get tankdeadstock
        public double GetTankDeadStock(int id)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankDeadStock = _Context.Tank.Where(p => p.TankId == id).Select(p => p.Height_Deadstock).FirstOrDefault();
                    if (tankDeadStock != null)
                    {
                        return tankDeadStock;
                    }
                    else
                    {
                        return 0;
                    }

                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        //get Max Volume
        public double GetMaxVolume(int id)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var maxVolume = _Context.Tank.Where(p => p.TankId == id).Select(p => p.VolumeSafeCapacity).FirstOrDefault();
                    if (maxVolume != null)
                    {
                        return maxVolume;
                    }
                    else
                    {
                        return 0;
                    }

                }
                catch (Exception)
                {
                    throw;
                }
            }
        }


    }
}
