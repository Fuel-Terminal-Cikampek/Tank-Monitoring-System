using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TMS.Web.Areas.Identity.Data;
using CSL.Web.Models;
using Microsoft.AspNetCore.Identity;
using CSL.Web;
using TMS.Web.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class AppRolesController : Controller
    {
        private readonly TMSContext _context;
        private readonly RoleManager<AppRole> _roleManager;
        public AppRolesController(TMSContext context, RoleManager<AppRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        // GET: AppRoles
        public IActionResult Index()
        {
            return View();
        }

        //Get : Json list Roles
        [HttpPost]
        public IActionResult LoadData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;

                // Get all roles
                var rolesQuery = _roleManager.Roles.AsQueryable();

                // Search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    rolesQuery = rolesQuery.Where(r => 
                        r.Name.Contains(searchValue) || 
                        (r.Create_By != null && r.Create_By.Contains(searchValue)));
                }

                // Total count
                recordsTotal = rolesQuery.Count();

                // ✅ Sorting - perbaiki untuk mendukung semua kolom
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    switch (sortColumn.ToLower())
                    {
                        case "name":
                            rolesQuery = sortColumnDirection.ToLower() == "asc" 
                                ? rolesQuery.OrderBy(r => r.Name) 
                                : rolesQuery.OrderByDescending(r => r.Name);
                            break;
                        case "create_by":
                            rolesQuery = sortColumnDirection.ToLower() == "asc" 
                                ? rolesQuery.OrderBy(r => r.Create_By) 
                                : rolesQuery.OrderByDescending(r => r.Create_By);
                            break;
                        case "create_time":
                            rolesQuery = sortColumnDirection.ToLower() == "asc" 
                                ? rolesQuery.OrderBy(r => r.Create_Time) 
                                : rolesQuery.OrderByDescending(r => r.Create_Time);
                            break;
                        default:
                            rolesQuery = rolesQuery.OrderBy(r => r.Name);
                            break;
                    }
                }
                else
                {
                    rolesQuery = rolesQuery.OrderBy(r => r.Name);
                }

                // Paging
                var data = rolesQuery.Skip(skip).Take(pageSize).ToList();

                // Map to DTO with camelCase property names
                var result = data.Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    createBy = r.Create_By ?? "",
                    createTime = r.Create_Time
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsFiltered = recordsTotal,
                    recordsTotal = recordsTotal,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new { draw = 0, recordsFiltered = 0, recordsTotal = 0, data = new List<object>(), error = ex.Message });
            }
        }


        // GET: AppRoles/AddOrEdit/5
        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(string id = null)
        {
            if (id == null)
            {
                return View(new AppRole());
            }

            else
            {
                var appRole = await _context.AppRoles.FindAsync(id);
                if (appRole == null)
                {
                    return NotFound();
                }
                return View(appRole);
            }
        }

        // POST: AppRoles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(string id, [Bind("Id,Name")] AppRole appRole)
        {
            // ✅ AUTHORIZATION: Only admins can create/update roles
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            try
            {
                var RoleFinder = _context.AppRoles.FirstOrDefaultAsync(x => x.Name == appRole.Name);
                if (RoleFinder.Result == null)
                {
                    appRole.Create_By = User.Identity.Name;
                    appRole.Create_Time = DateTime.Now;
                    _context.Add(appRole);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    appRole.Update_By = User.Identity.Name;
                    appRole.Update_Time = DateTime.Now;
                    _context.Update(appRole);
                    await _context.SaveChangesAsync();
                }
                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.AppRoles.ToList()) });
            }
            catch
            {
                return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", appRole) });
            }

        }


        //// GET: AppRoles/Delete/5
        //public async Task<IActionResult> Delete(string id)
        //{
        //    if (id == null || _context.AppRoles == null)
        //    {
        //        return NotFound();
        //    }

        //    var appRole = await _context.AppRoles
        //        .FirstOrDefaultAsync(m => m.Id == id);
        //    if (appRole == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(appRole);
        //}

        //// POST: AppRoles/Delete/5
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(string id)
        //{
        //    if (_context.AppRoles == null)
        //    {
        //        return Problem("Entity set 'CSFWebContext.AppRoles'  is null.");
        //    }
        //    var appRole = await _context.AppRoles.FindAsync(id);
        //    if (appRole != null)
        //    {
        //        _context.AppRoles.Remove(appRole);
        //    }

        //    await _context.SaveChangesAsync();
        //    return RedirectToAction(nameof(Index));
        //}

        //private bool AppRoleExists(string id)
        //{
        //  return _context.AppRoles.Any(e => e.Id == id);
        //}
    }
}