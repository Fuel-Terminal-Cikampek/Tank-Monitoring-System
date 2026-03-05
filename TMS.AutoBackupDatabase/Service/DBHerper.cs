using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.ATGService
{
    public class DBHerper
    {
        private AppDbContext _Context;

        private DbContextOptions<AppDbContext> GetAllOptions()
        {
            var optionBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionBuilder.UseSqlServer(AppSetting.ConnectionString);
            return optionBuilder.Options;
        }

        public List<Tank> GetAllTank()
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
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get All tank: {ex.Message}");
                }
            }
        }

        public Tank GetTankByName(string tankName)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    Tank tank = _Context.Tank.FirstOrDefault(x => x.Tank_Name == tankName);
                    if (tank != null)
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

        public Product GetProductById(string productCode)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    Product product = _Context.Master_Product.FirstOrDefault(x => x.Product_Code == productCode);
                    if (product != null)
                    {
                        return product;
                    }
                    else
                    {
                        return new Product();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        // ✅ TAMBAHKAN METHOD BARU - Filter berdasarkan Tank_Name dan tanggal
        public List<TankHistorical> GetTankHistoricalByTankName(DateTime range, string tankName)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankHistorical = _Context.Tank_Historical
                        .Where(t => t.Tank_Number == tankName
                                 && t.TimeStamp.HasValue
                                 && t.TimeStamp.Value.Year == range.Year
                                 && t.TimeStamp.Value.Month == range.Month
                                 && t.TimeStamp.Value.Day == range.Day)
                        .OrderBy(t => t.TimeStamp)
                        .ToList();

                    return tankHistorical ?? new List<TankHistorical>();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error getting historical data for {tankName}: {ex.Message}");
                }
            }
        }

        // ✅ TAMBAHKAN METHOD BARU - Filter berdasarkan range tanggal
        public List<TankHistorical> GetTankHistoricalByDateRange(string tankName, DateTime startDate, DateTime endDate)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankHistorical = _Context.Tank_Historical
                        .Where(t => t.Tank_Number == tankName 
                                 && t.TimeStamp >= startDate 
                                 && t.TimeStamp <= endDate)
                        .OrderBy(t => t.TimeStamp)
                        .ToList();

                    return tankHistorical ?? new List<TankHistorical>();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error getting historical data for {tankName}: {ex.Message}");
                }
            }
        }

        // ✅ METHOD UNTUK HAPUS DATA HISTORICAL
        public async Task ClearHistoricalTank(DateTime range, string tankName)
        {
            using (var context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var historicalData = await context.Tank_Historical
                        .Where(d => d.TimeStamp.HasValue
                                    && d.TimeStamp.Value.Date == range.Date
                                    && d.Tank_Number == tankName)
                        .ToListAsync();

                    if (historicalData.Any())
                    {
                        context.Tank_Historical.RemoveRange(historicalData);
                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error clearing historical data for {tankName}: {ex.Message}", ex);
                }
            }
        }

        public async void ClearHistoricalBefore(DateTime range)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var oldData = _Context.Tank_Historical.Where(d => d.TimeStamp < range);
                    if (oldData != null)
                    {
                        _Context.Tank_Historical.RemoveRange(oldData);
                        await _Context.SaveChangesAsync();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        // ✅ METHOD BARU: Hapus data historical berdasarkan range bulan
        public async Task<int> ClearHistoricalByMonthRange(DateTime startDate, DateTime endDate)
        {
            using (var context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var historicalData = await context.Tank_Historical
                        .Where(d => d.TimeStamp.HasValue
                                    && d.TimeStamp.Value >= startDate 
                                    && d.TimeStamp.Value < endDate) // Tidak termasuk endDate
                        .ToListAsync();

                    int deletedCount = historicalData.Count;

                    if (historicalData.Any())
                    {
                        context.Tank_Historical.RemoveRange(historicalData);
                        await context.SaveChangesAsync();
                    }

                    return deletedCount;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error clearing historical data from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}: {ex.Message}", ex);
                }
            }
        }

        // ✅ METHOD BARU: Hapus data lebih lama dari retention period
        public async Task<Dictionary<string, long>> ClearOldHistoricalData(int retentionMonths)
        {
            using (var context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    DateTime cutoffDate = DateTime.Now.AddMonths(-retentionMonths);
                    DateTime deleteBeforeDate = new DateTime(cutoffDate.Year, cutoffDate.Month, 1, 0, 0, 0);

                    var result = new Dictionary<string, long>(); // ✅ Changed to long

                    var allTanks = await context.Tank.ToListAsync();

                    long totalDeleted = 0; // ✅ Changed to long

                    foreach (var tank in allTanks)
                    {
                        var oldData = await context.Tank_Historical
                            .Where(d => d.Tank_Number == tank.Tank_Name 
                                        && d.TimeStamp.HasValue
                                        && d.TimeStamp.Value < deleteBeforeDate)
                            .ToListAsync();

                        long tankDeletedCount = oldData.Count; // ✅ Changed to long

                        if (oldData.Any())
                        {
                            context.Tank_Historical.RemoveRange(oldData);
                            totalDeleted += tankDeletedCount;
                            result[tank.Tank_Name] = tankDeletedCount;
                        }
                        else
                        {
                            result[tank.Tank_Name] = 0;
                        }
                    }

                    await context.SaveChangesAsync();

                    result["TOTAL"] = totalDeleted;
                    result["CutoffDate"] = deleteBeforeDate.Ticks; // ✅ Now works!

                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error clearing old historical data (retention: {retentionMonths} months): {ex.Message}", ex);
                }
            }
        }
    }
}
