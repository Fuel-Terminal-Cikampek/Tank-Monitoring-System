using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using CSL.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using TMS.Web.Areas.Identity.Data;
using TMS.Web;
using TMS.Web.Models;

namespace CSL.Web.Controllers
{
    public class RolesController : Controller
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly TMSContext _context;
        public RolesController(TMSContext context, RoleManager<AppRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }
        // GET: Roles
        public ActionResult Index()
        {
            return View();
        }

        public IActionResult LoadData()
        {
            try
            {
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();// Skip number of Rows count
                var length = Request.Form["length"].FirstOrDefault();// Paging Length 10,20
                var sortColumn = Request.Form["column[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();// sort column name
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault(); // Sort Column Direction (asc, desc)
                var searchValue = Request.Form["search[value]"].FirstOrDefault(); //Search value from (Search box)
                int pageSize = length != null ? Convert.ToInt32(start) : 0; //Paging size (10, 20,50, 100)
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;

                //geting all.data
                var role = (from a in _context.Roles
                             select a);

                //sorting
                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    role = role.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                //searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    role = role.Where(m => m.Name.Contains(searchValue));
                }

                //total number of row counts
                recordsTotal = role.Count();
                //Paging
                var data = role.Skip(skip).Take(pageSize).ToList();
                //returning Json Data
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }

        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(string id = "")
        {
            if (id == "" || id == null)//flagged as insert
            {
                return View(new IdentityRole());
            }
            else
            {
                var role = await _context.Roles.FindAsync(id);
                return View(role);
            }
        }
        //POST: Section/AddOrEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(Guid? id, [Bind("Name")] AppRole identityRole)
        {
            if (ModelState.IsValid)
            {
                if (RoleExists(id))
                {
                    try
                    {
                        var rl = await _roleManager.FindByIdAsync(id?.ToString());
                        rl.Name = identityRole.Name;
                        var result = await _roleManager.UpdateAsync(rl);
                        if (result.Succeeded)
                        {
                            return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Roles.ToList()) });
                        }
                        else
                        {
                            foreach (var error in result.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", identityRole) });
                        }
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!RoleExists(identityRole.Id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else //new
                {
                    _context.Add(identityRole);
                    var result = await _roleManager.CreateAsync(identityRole);
                    if (result.Succeeded)
                    {
                        return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Roles.ToList()) });
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", identityRole) });
                    }
                }
            }
            else
            {
                return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", identityRole) });
            }
        }

        private bool RoleExists(Guid? id)
        {
            if (!id.HasValue)
                return false;
            return _context.Roles.Any(e => e.Id == id.Value);
        }
    }
}
