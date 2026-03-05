using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.TankDipPosting
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
        public List<TankTicket> GetTankTicketNotPosted()
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tankTickets = _Context.Tank_Ticket.Where(t=>t.Is_Upload_Success != true).ToList();
                    if (tankTickets.Count() >0)
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
        
        public void UpdateTankTicket(TankTicket ticket)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    _Context.Update(ticket);
                    _Context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Update TankTicket{ticket.Ticket_Number}: {ex.Message}");
                }

            }

        }
        public TankLiveData GetTankByName(string tankNumber)
        {
            using (_Context = new AppDbContext(GetAllOptions()))
            {
                try
                {
                    var tank = _Context.Tank_Live_Data.FirstOrDefault(t => t.Tank_Number == tankNumber);
                    return tank;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception Get Tank LiveData By Number {tankNumber}: {ex.Message}");
                }
            }
        }
    }
}
