using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using TMS.Web.Areas.Identity.Data;
using CSL.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using TMS.Web.Models;
using Microsoft.Win32;
using TMS.Web.Views.AppUsers;
using TMS.Web;
using TMS.Web.Authorization;
using TMS.Web.Services;

namespace CSL.Web.Controllers
{
    public class AppUsersController : Controller
    {
        private readonly TMSContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;

        public AppUsersController(TMSContext context, UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

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
                var searchValue = Request.Form["search[value]"].FirstOrDefault(); // Search Value from (Search box)
                int pageSize = length != null ? Convert.ToInt32(length) : 0; //Paging Size (10, 20, 50,100)  
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int recordsTotal = 0;
                var allUsers = _userManager.Users.ToList();

                // Get all roles
                var allRoles = _roleManager.Roles.ToList();

                // Join the users and roles based on the roles assigned to each user
                // NOTE: aspnet_Users table only has UserName, not FullName/Email/PhoneNumber
                // Those fields are Identity properties that are Ignored in DbContext
                var listUserRole = (from user in allUsers
                                    select new
                                    {
                                        user.Id,
                                        UserName = user.UserName ?? "",  // Use UserName from database
                                        Role = GetRoles(user),
                                        Email = user.Email ?? "",  // Identity property (may be empty)
                                        PhoneNumber = user.PhoneNumber ?? "",  // Identity property (may be empty)
                                    });

                //Search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    listUserRole = listUserRole.Where(m =>
                        (m.UserName != null && m.UserName.Contains(searchValue)) ||
                        (m.Email != null && m.Email.Contains(searchValue)) ||
                        (m.PhoneNumber != null && m.PhoneNumber.Contains(searchValue)));
                }

                //total number of rows counts   
                recordsTotal = listUserRole.Count();
                //Paging   
                var data = listUserRole.Skip(skip).Take(pageSize).ToList();
                //Returning Json Data  
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data });
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string GetRoles(AppUser user)
        {
            string resultRoles = "";
            var roleUser = _userManager.GetRolesAsync(user).Result;
            if (roleUser.Count > 0)
            {
                resultRoles = roleUser[0].ToString();
            }
            return resultRoles;
        }

        // GET: AppRoles/AddOrEdit/5
        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(Guid? id = null)
        {
            AppUser user = null;
            if (id.HasValue)
            {
                user = _context.AppUsers.FirstOrDefault(x => x.Id == id.Value);
            }
            populateRoleSection();
            if (user == null)
            {

                return View(new Register());
            }

            else
            {
                var register = new Register();
                register.Id = user.Id;
                register.FullName = user.FullName;
                register.UserName = user.UserName;
                register.Email = user.Email;
                register.Password = "";
                register.ConfirmPassword = "";
                return View(register);
            }
        }

        // POST: AppUsers/AddOrEdit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(int id, [Bind("Id,FullName,UserName,Email, RoleName,Password,ConfirmPassword,NewPassword")] Register register)
        {
            // ✅ AUTHORIZATION: Only admins can create/update users
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            var UserExist = await _userManager.FindByIdAsync(register.Id.ToString());
            if (UserExist == null)
            {
                var newuser = new AppUser { UserName = register.UserName, Email = register.Email, FullName = register.FullName };
                var result = await _userManager.CreateAsync(newuser, register.Password);
                if (result.Succeeded)
                {
                    var user = await _context.AppUsers.FirstOrDefaultAsync(x => x.UserName == register.UserName);
                    if (user != null)
                    {
                        // ✅ Create aspnet_Membership record with encrypted password (PasswordFormat=2)
                        var (encryptedPassword, passwordSalt) = LegacyPasswordHasher.EncryptPasswordLegacy(register.Password);
                        var membership = new AspnetMembership
                        {
                            UserId = user.Id,
                            ApplicationId = user.ApplicationId,
                            Password = encryptedPassword,
                            PasswordFormat = 2, // 3DES Encrypted
                            PasswordSalt = passwordSalt, // Random 16-byte salt
                            Email = register.Email,
                            LoweredEmail = register.Email?.ToLower(),
                            IsApproved = true,
                            IsLockedOut = false,
                            CreateDate = DateTime.Now,
                            LastLoginDate = DateTime.Now,
                            LastPasswordChangedDate = DateTime.Now,
                            LastLockoutDate = new DateTime(1754, 1, 1), // SQL Server min date
                            FailedPasswordAttemptCount = 0,
                            FailedPasswordAttemptWindowStart = new DateTime(1754, 1, 1),
                            FailedPasswordAnswerAttemptCount = 0,
                            FailedPasswordAnswerAttemptWindowStart = new DateTime(1754, 1, 1)
                        };
                        _context.AspnetMemberships.Add(membership);
                        await _context.SaveChangesAsync();

                        // ✅ FIX: RoleName from form contains GUID, not Name
                        // Try to parse as GUID first, fallback to Name search
                        AppRole roleExists = null;
                        if (Guid.TryParse(register.RoleName, out Guid roleId))
                        {
                            roleExists = _context.Roles.FirstOrDefault(x => x.Id == roleId);
                        }
                        else
                        {
                            roleExists = _context.Roles.FirstOrDefault(x => x.Name == register.RoleName);
                        }

                        if (roleExists != null)
                        {
                            //Add the role to the user
                            var userRole = new IdentityUserRole<Guid>
                            {
                                UserId = user.Id,
                                RoleId = roleExists.Id
                            };
                            _context.UserRoles.Add(userRole);
                            await _context.SaveChangesAsync();
                        }
                    }


                    return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.AppUsers.ToList()) });
                }
                return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", register) });
            }
            else
            {
                // ✅ FIX: RoleName from form contains GUID, not Name
                // Try to parse as GUID first, fallback to Name search
                AppRole roleExists = null;
                if (Guid.TryParse(register.RoleName, out Guid roleId))
                {
                    roleExists = _context.Roles.FirstOrDefault(x => x.Id == roleId);
                }
                else
                {
                    roleExists = _context.Roles.FirstOrDefault(x => x.Name == register.RoleName);
                }

                if (roleExists != null)
                {
                    // Add the role to the user
                    var userRoles = _context.UserRoles.FirstOrDefault(u => u.UserId == UserExist.Id);
                    if (userRoles != null)
                    {
                        _context.UserRoles.Remove(userRoles);
                        await _context.SaveChangesAsync();
                    }
                    var userRole = new IdentityUserRole<Guid>
                    {
                        UserId = UserExist.Id,
                        RoleId = roleExists.Id
                    };
                    UserExist.UserName = register.UserName;
                    UserExist.FullName = register.FullName;
                    UserExist.Email = register.Email;
                    _context.UserRoles.Add(userRole);
                    await _context.SaveChangesAsync();

                }
                if (register.Password == "" || register.NewPassword == "" || register.Password == null || register.NewPassword == null)
                {
                    return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.AppUsers.ToList()) });
                }
                var result = await _userManager.ChangePasswordAsync(UserExist, register.Password, register.NewPassword);

                if (result.Succeeded)
                {
                    return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.AppUsers.ToList()) });
                }
                return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", register) });
            }
        }

        //get roles
        private void populateRoleSection(object selectedSection = null)
        {
            var AppRoles = (from p in _context.AppRoles select p).ToList();
            var approleip = new AppRole()
            {
                Id = Guid.Empty,
                Name = "--- Select Role ---"
            };
            AppRoles.Insert(0, approleip);
            ViewBag.RolesId = AppRoles;
        }

        private bool UsernameExists(string username)
        {
            return _context.AppUsers.Any(e => e.UserName == username);
        }

        private bool IdExists(Guid id)
        {
            return _context.AppUsers.Any(e => e.Id == id);
        }

        // POST: User/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            // ✅ AUTHORIZATION: Only admins can delete users
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            var user = await _context.AppUsers.FindAsync(id);
            if (user != null)
            {
                // ✅ Delete in correct order to avoid FK constraint violations

                // 1. Delete from aspnet_UsersInRoles first
                var userRoles = _context.UserRoles.Where(ur => ur.UserId == id).ToList();
                if (userRoles.Any())
                {
                    _context.UserRoles.RemoveRange(userRoles);
                    await _context.SaveChangesAsync();
                }

                // 2. Delete from aspnet_Membership
                var membership = await _context.AspnetMemberships.FindAsync(id);
                if (membership != null)
                {
                    _context.AspnetMemberships.Remove(membership);
                    await _context.SaveChangesAsync();
                }

                // 3. Finally delete from aspnet_Users
                _context.AppUsers.Remove(user);
                await _context.SaveChangesAsync();
            }

            return Json(new { html = Helper.RenderRazorViewString(this, "_ViewAll", _context.AppUsers.ToList()) });
        }
    }
}
