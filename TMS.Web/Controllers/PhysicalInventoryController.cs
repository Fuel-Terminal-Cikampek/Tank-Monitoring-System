using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CSL.Web.Models;
using TMS.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using TMS.Web.Areas.Identity.Data;

namespace TMS.Web.Controllers
{
    public class PhysicalInventoryController : Controller
    {
        private readonly TMSContext _context;

        //
        public PhysicalInventoryController(TMSContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            populateTanks();
            return View();
        }
        //Get: data to datatable
        public IActionResult LoadData()
        {
            try
            {

                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();//Skip number of rows count
                var length = Request.Form["length"].FirstOrDefault();//paging length 10, 20
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();// sort column name
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();//sort column direction (asc, desc)
                var searchValue = Request.Form["search[value]"].FirstOrDefault();//search value from (search box)
                int pageSize = length != null ? Convert.ToInt32(length) : 0;//paging size (10, 20, 50, 100)
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;
                //get all data
                var tankticket = (from t in _context.Tank_Ticket
                                  join p in _context.Tank on t.Tank_Id equals p.TankId
                                  select new
                                  {
                                      t.Id,
                                      p.Tank_Name,
                                      t.Ticket_Number,
                                      t.Timestamp,
                                      t.Operation_Type,
                                      t.Operation_Status,
                                      t.Measurement_Method,
                                      t.LiquidLevel,
                                      t.WaterLevel,
                                      t.LiquidTemperature,
                                      t.LiquidDensity,
                                      t.Is_Upload_Success,
                                  }).Where(t=>t.Operation_Type =="PI");
                //sorting
                if (!string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection))
                {
                    tankticket = tankticket.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                //search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    tankticket = tankticket.Where(m => m.Tank_Name.Contains(searchValue));
                }

                //total number of rows counts
                recordsTotal = tankticket.Count();
                //paging
                var data = tankticket.Skip(skip).Take(pageSize).ToList();
                //returning json data
                return Json(new { draw = draw, recordsFilterd = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void populateTanks(object SelectList = null)
        {
            List<Tank> tanks = new List<Tank>();
            tanks = (from t in _context.Tank select t).ToList();
            var tankip = new Tank()
            {
                TankId = 0,
                Tank_Name = "---- Select Tank ----"
            };
            tanks.Insert(0, tankip);
            ViewBag.TankId = tanks;
        }
    }
}
