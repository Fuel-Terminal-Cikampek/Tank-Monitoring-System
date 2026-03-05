using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.Formula.Functions;
using TMS.Models;

namespace TMS.AutoPI
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
        public List<Tank> GetAllTankActive()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tanks = _Context.Tank.OrderBy(t=>t.Tank_Name).ToList();
                    if (tanks != null)
                    {
                        return tanks;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get All tank :{ex.Message}");
                }
            }
        }
        public TankHistorical GetHistoricalPI(int id)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    DateTime now = DateTime.Now;

                    DateTime PIdate = now.AddDays(-1);
                    var date = PIdate.Date.AddHours(23).AddMinutes(59).AddSeconds(00);
                    var liveDatas = _Context.Tank_Historical.Where(t => t.Id == id &&
                    t.TimeStamp.Value.Year == date.Year &&
                    t.TimeStamp.Value.Month == date.Month &&
                    t.TimeStamp.Value.Day == date.Day &&
                    t.TimeStamp.Value.Hour == date.Hour &&
                    t.TimeStamp.Value.Minute == date.Minute);
                    var liveData = liveDatas.FirstOrDefault();
                    if (liveData != null)
                    {
                        return liveData;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get tank LiveData :{ex.Message}");
                }
            }
        }
        public TankLiveData GetLiveDataByTankName(string tankName)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var liveData = _Context.Tank_Live_Data.FirstOrDefault(t=>t.Tank_Number == tankName);
                    if (liveData != null)
                    {
                        return liveData;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get tank LiveData :{ex.Message}");
                }
            }
        }
        public void AddTankTicket(TankTicket ticket)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {

                    _Context.Add(ticket);
                    _Context.SaveChanges();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get add TankTicket :{ex.Message}");
                }
            }
        }

        public List<TankTicket> GetPI()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {

                    var PiTickets = _Context.Tank_Ticket.Where(t => t.Timestamp >= DateTime.Now.Date.AddDays(-7) && t.Operation_Type == "PI").ToList();
                   
                    return PiTickets;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get PI Error :{ex.Message}");
                }
            }
        }

        public List<TankTicket> CheckTankPINow(DateTime yesterday)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    yesterday = yesterday.Date.AddHours(23).AddMinutes(59).AddSeconds(00);
                    var tickets = _Context.Tank_Ticket.Where(t => ((DateTime)t.Timestamp).Year == yesterday.Year
                    && ((DateTime)t.Timestamp).Month == yesterday.Month
                    && ((DateTime)t.Timestamp).Day == yesterday.Day
                    && ((DateTime)t.Timestamp).Hour == yesterday.Hour
                    && ((DateTime)t.Timestamp).Minute == yesterday.Minute
                    && t.Operation_Type == "PI").ToList();
                    if( tickets == null )
                    {
                        tickets = new List<TankTicket>();
                    }
                    return tickets;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get PI Ready :{ex.Message}");
                }
            }
        }
        public async void UpdateTankTicket(TankTicket ticket)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    _Context.Update(ticket);
                    await _Context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Update TankTicket{ticket.Ticket_Number}: {ex.Message}");
                }

            }

        }
        public List<TankTicket> tankTicketPIUnUploaded()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankTickets = _Context.Tank_Ticket.Where(t => t.Is_Upload_Success != true && t.Operation_Type=="PI" && t.Timestamp >= DateTime.Now.Date.AddDays(-1)).ToList();
                    if (tankTickets.Count() > 0)
                    {
                        return tankTickets;
                    }
                    else
                    {
                        return new List<TankTicket>();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Get Tankticket Not Posted{ex.Message}");
                }
            }
        }

        public List<TankTicket> getTicketPriority()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    // NOTE: Tank_Id filter removed - Tank_Id is NotMapped and cannot be used in LINQ queries
                    // If specific tanks are needed, replace with Tank_Number filter: && (t.Tank_Number == "T23" || t.Tank_Number == "T32" || ...)
                    var tankTickets = _Context.Tank_Ticket.Where(t => t.Is_Upload_Success != true && t.Operation_Type == "PI" && t.Timestamp >= DateTime.Now.Date.AddDays(-1)).ToList();
                    if (tankTickets.Count() > 0)
                    {
                        return tankTickets;
                    }
                    else
                    {
                        return new List<TankTicket>();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Get Tankticket Not Posted{ex.Message}");
                }
            }
        }
        public int CountTankPI(string type)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    DateTime now = DateTime.Now;
                    int currentYear = now.Year;
                    int currentMonth = now.Month;

                    int tickets = _Context.Tank_Ticket
                    .Where(t => t.Operation_Type == type && t.Timestamp.HasValue && t.Timestamp.Value.Year == currentYear)
                    .Count();

                    return tickets;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Count TankTicket :{ex.Message}");
                }
            }
        }

        public List<TankTicket> CheckTankCHKNow(string tankName, DateTime lastRecord)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tickets = _Context.Tank_Ticket.Where(t => t.Tank_Number == tankName
                    && ((DateTime)t.Timestamp).Year == lastRecord.Year
                    && ((DateTime)t.Timestamp).Month == lastRecord.Month
                    && ((DateTime)t.Timestamp).Day == lastRecord.Day
                    && ((DateTime)t.Timestamp).Hour == lastRecord.Hour
                    && ((DateTime)t.Timestamp).Minute == lastRecord.Minute
                    && t.Operation_Type == "CHK").ToList();
                    return tickets;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get add TankTicket :{ex.Message}");
                }
            }
        }
        public Tank GetTankByName(string tankName)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tank = _Context.Tank.FirstOrDefault(t => t.Tank_Name == tankName);
                    return tank;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get Tank By Name {tankName} :{ex.Message}");
                }

            }
        }
    }
}
