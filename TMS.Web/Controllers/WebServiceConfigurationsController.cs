using CSL.Web;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using TMS.Models;
using System.Linq.Dynamic.Core;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class WebServiceConfigurationsController : Controller
    {
        private readonly TMSContext _context;

        public WebServiceConfigurationsController(TMSContext context)
        {
            _context = context;
        }

        // GET: WebServiceConfigurations
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult LoadData()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();// Skip number of Rows count  
                var length = Request.Form["length"].FirstOrDefault(); // Paging Length 10,20  
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault(); // Sort Column Name  
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();// Sort Column Direction (asc, desc)  
                var searchValue = Request.Form["serviceNameSearch"].FirstOrDefault(); // Search Value from (Search box)  
                int pageSize = length != null ? Convert.ToInt32(length) : 0; //Paging Size (10, 20, 50,100)  
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;

                // getting all data  
                var webServiceConfiguration = (from p in _context.WebServiceConfigurations select p);//LINQ

                //Sorting
                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    webServiceConfiguration = webServiceConfiguration.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    // Default sorting by id if no sort specified
                    webServiceConfiguration = webServiceConfiguration.OrderBy(x => x.Id);
                }
                //Search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    webServiceConfiguration = webServiceConfiguration.Where(m => m.ServiceName.Contains(searchValue));
                }

                //total number of rows counts
                recordsTotal = webServiceConfiguration.Count();
                //Paging
                var data = webServiceConfiguration.Skip(skip).Take(pageSize).ToList();
                //Returning Json Data  
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }


        // GET: WebServiceConfigurations/AddOrEdit
        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(int id = 0)
        {
            if (id == 0)//flagged as insert
            {
                return View(new WebServiceConfiguration());
            }
            else
            {
                var webServiceConfiguration = await _context.WebServiceConfigurations.FindAsync(id);
                if (webServiceConfiguration == null)
                {
                    return NotFound();
                }
                return View(webServiceConfiguration);
            }
        }


        // POST: WebServiceConfigurations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(int id, [Bind("Id,ServiceName,Status,URL,Username,Password")] WebServiceConfiguration webServiceConfiguration)
        {

            if (ModelState.IsValid)
            {
                if (id == 0)
                {
                    webServiceConfiguration.CreateBy = User.Identity.Name;
                    webServiceConfiguration.CreatedTimeStamp = DateTime.Now;
                    _context.Add(webServiceConfiguration);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    try
                    {
                        webServiceConfiguration.UpdateBy = User.Identity.Name;
                        webServiceConfiguration.UpdatedTimeStamp = DateTime.Now;
                        _context.Update(webServiceConfiguration);
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!WebServiceConfigurationExists(webServiceConfiguration.Id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.WebServiceConfigurations.ToList()) });
            }
            var errors = ModelState.Where(x => x.Value.Errors.Any())
                .Select(x => new { x.Key, x.Value.Errors });

            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", webServiceConfiguration) });
        }


        // POST: WebServiceConfigurations/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // ✅ AUTHORIZATION: Only admins can delete
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            var webServiceConfiguration = await _context.WebServiceConfigurations.FindAsync(id);
            _context.WebServiceConfigurations.Remove(webServiceConfiguration);
            await _context.SaveChangesAsync();
            return Json(new { html = Helper.RenderRazorViewString(this, "_ViewAll", _context.WebServiceConfigurations.ToList()) });
        }


        private bool WebServiceConfigurationExists(int id)
        {
            return _context.WebServiceConfigurations.Any(e => e.Id == id);
        }


    }
}
