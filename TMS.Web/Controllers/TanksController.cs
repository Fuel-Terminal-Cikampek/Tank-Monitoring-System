using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CSL.Web.Models;
using CSL.Web;
using Microsoft.AspNetCore.Authorization;
using TMS.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class TanksController : Controller
    {
        private readonly TMSContext _context;

        public TanksController(TMSContext context)
        {
            _context = context;
        }

        // GET: Tanks
        public IActionResult Index()
        {
            return View();
        }
        //GET  data to datatable
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

                var tanks = _context.Tank.ToList();
                var liveData = _context.Tank_Live_Data.ToList();

                //get all data
                // NOTE: Height_Vol_Max already contains volume in Liters (no calculation needed)
                var tank = (from t in _context.Tank
                            join p in _context.Master_Product on t.Product_ID equals p.Product_Code
                            join ld in _context.Tank_Live_Data on t.Tank_Name equals ld.Tank_Number into liveDataJoin
                            from ld in liveDataJoin.DefaultIfEmpty()
                            select new
                            {
                                t.Tank_Name,
                                p.Product_Name,
                                // Height_Vol_Max is already in Liters (despite misleading column name)
                                TankVolume = (double?)t.Height_Vol_Max,
                                t.Tank_Height,
                                t.Tank_Diameter,
                                t.Tape,
                                t.Bob,
                                t.Blank,
                                t.IsUsed,
                                t.IsAutoPI,
                                IsManualDensity = t.IsManualDensity ?? false,
                                ManualLabDensity = t.ManualLabDensity ?? 0,
                                IsManualTemp = t.IsManualTemp ?? false,
                                ManualTemp = t.ManualTemp ?? 0,
                                IsManualTestTemp = t.IsManualTestTemp ?? false,
                                ManualTestTemp = t.ManualTestTemp ?? 0,
                                IsManualWaterLevel = t.IsManualWaterLevel ?? false,
                                ManualWaterLevel = t.ManualWaterLevel ?? 0
                            });
                //Sorting
                tank = tank.AsEnumerable() // Convert to IEnumerable untuk custom sorting
                            .OrderBy(t => {
                                var match = System.Text.RegularExpressions.Regex.Match(t.Tank_Name ?? "", @"T-(\d+)");
                                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                            })
                            .AsQueryable();

                //Search  
                if (!string.IsNullOrEmpty(searchValue))
                {
                    tank = tank.Where(m => m.Product_Name.Contains(searchValue));
                }

                //total number of rows counts
                recordsTotal = tank.Count();
                //Paging
                var data = tank.Skip(skip).Take(pageSize).ToList();

                // Debug logging
                Console.WriteLine($"Tank LoadData - Total records: {recordsTotal}");
                if (data.Count > 0)
                {
                    var firstItem = data.First();
                    Console.WriteLine($"Tank LoadData - First item Tank_Name: {firstItem.Tank_Name}");
                }

                //Returning Json Data with property name preservation
                return Json(new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = data },
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = null // Preserve property name casing
                    });
            }
            catch (Exception)
            {
                throw;
            }
        }
        // GET: Tanks/AddOrEdit
        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(string id = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                populateProductSection();
                return View(new Tank());
            }

            else
            {
                populateProductSection();
                var tank = await _context.Tank.FindAsync(id);
                if (tank == null)
                {
                    return NotFound();
                }
                return View(tank);
            }
        }

        // POST: Tanks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit(string id, [Bind("Tank_Name,Product_ID,TankVolume,Tank_Height,Tank_Diameter,VolumeSafeCapacity,Height_Deadstock,Blank,Tape,Bob,IsUsed,IsAutoPI,IsManualTemp,ManualTemp,IsManualTestTemp,ManualTestTemp,IsManualDensity,ManualLabDensity,IsManualWaterLevel,ManualWaterLevel,IsManualLevel,ManualLevel,UseAlarmLL,LevelLL,UseAlarmL,LevelL,UseAlarmH,LevelH,UseAlarmHH,LevelHH,UseAlarmTempL,TempL,UseAlarmTempH,TempH,DefaultKLperH")] Tank tank)
        {

            try
            {
                // Debug logging
                Console.WriteLine($"AddOrEdit POST called - id: '{id}', tank.Tank_Name: '{tank.Tank_Name}', tank.Product_ID: '{tank.Product_ID}', tank.Group_Name: '{tank.Group_Name}'");

                // Validate Tank_Name
                if (string.IsNullOrEmpty(tank.Tank_Name))
                {
                    // If id is provided (EDIT mode), use id as Tank_Name
                    if (!string.IsNullOrEmpty(id))
                    {
                        tank.Tank_Name = id;
                        Console.WriteLine($"Using id as Tank_Name: '{tank.Tank_Name}'");
                    }
                    else
                    {
                        return Json(new {
                            isValid = false,
                            html = Helper.RenderRazorViewString(this, "AddOrEdit", tank),
                            errorMessage = "Tank Name is required"
                        });
                    }
                }

                // Set default values for NOT NULL columns that are not in the form
                // For NEW tank, get default values from first existing tank or use safe defaults
                if (string.IsNullOrEmpty(id))
                {
                    // Get Product_Name from selected Product_ID
                    string productName = null;
                    if (!string.IsNullOrEmpty(tank.Product_ID))
                    {
                        var product = _context.Master_Product.FirstOrDefault(p => p.Product_Code == tank.Product_ID);
                        if (product != null)
                        {
                            productName = product.Product_Name;
                            Console.WriteLine($"Selected Product: {product.Product_Code} - {product.Product_Name}");
                        }
                    }

                    // Set Group_Name - must exist in Tank_Group table (FK constraint)
                    if (string.IsNullOrEmpty(tank.Group_Name))
                    {
                        if (!string.IsNullOrEmpty(productName))
                        {
                            // Check if Product_Name exists in Tank_Group table using raw SQL
                            var connection = _context.Database.GetDbConnection();
                            var wasOpen = connection.State == System.Data.ConnectionState.Open;
                            if (!wasOpen) await connection.OpenAsync();

                            var command = connection.CreateCommand();
                            command.CommandText = $"SELECT Tank_Group FROM Tank_Group WHERE Tank_Group = @productName";
                            var param = command.CreateParameter();
                            param.ParameterName = "@productName";
                            param.Value = productName;
                            command.Parameters.Add(param);

                            var tankGroupExists = await command.ExecuteScalarAsync() as string;

                            if (!wasOpen) await connection.CloseAsync();

                            if (!string.IsNullOrEmpty(tankGroupExists))
                            {
                                // Product_Name MATCH dengan Tank_Group
                                tank.Group_Name = productName;
                                Console.WriteLine($"Setting Group_Name = Product_Name: {productName} (MATCH di Tank_Group)");
                            }
                            else
                            {
                                // Product_Name TIDAK ADA di Tank_Group - use default "PERTALITE"
                                tank.Group_Name = "PERTALITE";
                                Console.WriteLine($"WARNING: Product '{productName}' not in Tank_Group table. Using default Group_Name: PERTALITE");
                            }
                        }
                        else
                        {
                            // No product selected - use default "PERTALITE"
                            tank.Group_Name = "PERTALITE";
                            Console.WriteLine($"Warning: No Product selected, using default Group_Name: PERTALITE");
                        }
                    }

                    // Get template values for other NOT NULL fields from first tank (ordered by name for consistency)
                    var firstTank = _context.Tank.OrderBy(t => t.Tank_Name).FirstOrDefault();
                    if (firstTank != null)
                    {
                        Console.WriteLine($"Template tank for other fields: {firstTank.Tank_Name}");

                        // Use values from existing tank as template (except Group_Name - already set above)
                        if (string.IsNullOrEmpty(tank.Tank_Form)) tank.Tank_Form = firstTank.Tank_Form;
                        if (tank.Height_Safe_Capacity == 0) tank.Height_Safe_Capacity = firstTank.Height_Safe_Capacity;
                        if (tank.Height_Vol_Max == 0) tank.Height_Vol_Max = firstTank.Height_Vol_Max;
                        if (tank.Height_Point_Desk == 0) tank.Height_Point_Desk = firstTank.Height_Point_Desk;
                        if (tank.Height_Tank_Base == 0) tank.Height_Tank_Base = firstTank.Height_Tank_Base;
                        if (tank.Height_Deadstock == 0) tank.Height_Deadstock = firstTank.Height_Deadstock;
                        if (tank.Stretch_Coefficient == 0) tank.Stretch_Coefficient = firstTank.Stretch_Coefficient;
                        if (tank.Density_Calibrate == 0) tank.Density_Calibrate = firstTank.Density_Calibrate;
                        if (tank.RaisePerMM == 0) tank.RaisePerMM = firstTank.RaisePerMM;
                    }
                    else
                    {
                        // Fallback to safe defaults (this shouldn't happen if there are existing tanks)
                        if (string.IsNullOrEmpty(tank.Group_Name)) tank.Group_Name = "DEFAULT";
                        if (string.IsNullOrEmpty(tank.Tank_Form)) tank.Tank_Form = "CYLINDRICAL";
                    }

                    // Create new TankLiveData separately - not via navigation property
                    var newLiveData = new TankLiveData
                    {
                        Tank_Number = tank.Tank_Name,
                        Product_ID = tank.Product_ID  // CRITICAL: Set Product_ID to avoid NULL constraint error
                    };

                    tank.Create_By = User.Identity.Name;
                    tank.Create_Time = DateTime.Now;

                    Console.WriteLine($"BEFORE SaveChanges - Tank: Product_ID={tank.Product_ID}, Group_Name={tank.Group_Name}");
                    Console.WriteLine($"BEFORE SaveChanges - LiveData: Product_ID={newLiveData.Product_ID}");

                    _context.Add(tank);
                    _context.Add(newLiveData);
                    await _context.SaveChangesAsync();

                    // Check what was actually saved
                    var savedTank = await _context.Tank.FirstOrDefaultAsync(t => t.Tank_Name == tank.Tank_Name);
                    var savedLiveData = await _context.Tank_Live_Data.FirstOrDefaultAsync(t => t.Tank_Number == tank.Tank_Name);
                    Console.WriteLine($"AFTER SaveChanges - Tank: Product_ID={savedTank?.Product_ID}, Group_Name={savedTank?.Group_Name}");
                    Console.WriteLine($"AFTER SaveChanges - LiveData: Product_ID={savedLiveData?.Product_ID}");

                }
                else
                {
                    try
                    {
                        // EDIT mode: Update Group_Name based on selected Product (must exist in Tank_Group)
                        if (!string.IsNullOrEmpty(tank.Product_ID))
                        {
                            var product = _context.Master_Product.FirstOrDefault(p => p.Product_Code == tank.Product_ID);
                            if (product != null)
                            {
                                // Check if Product_Name exists in Tank_Group table using raw SQL
                                var connection = _context.Database.GetDbConnection();
                                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                                if (!wasOpen) await connection.OpenAsync();

                                var command = connection.CreateCommand();
                                command.CommandText = $"SELECT Tank_Group FROM Tank_Group WHERE Tank_Group = @productName";
                                var param = command.CreateParameter();
                                param.ParameterName = "@productName";
                                param.Value = product.Product_Name;
                                command.Parameters.Add(param);

                                var tankGroupExists = await command.ExecuteScalarAsync() as string;

                                if (!wasOpen) await connection.CloseAsync();

                                if (!string.IsNullOrEmpty(tankGroupExists))
                                {
                                    tank.Group_Name = product.Product_Name;
                                    Console.WriteLine($"EDIT: Updating Group_Name = Product_Name: {product.Product_Name} (MATCH)");
                                }
                                else
                                {
                                    // Product not in Tank_Group - set to default "PERTALITE"
                                    tank.Group_Name = "PERTALITE";
                                    Console.WriteLine($"EDIT: Product '{product.Product_Name}' not in Tank_Group. Setting Group_Name to default: PERTALITE");
                                }
                            }
                        }

                        // Get existing tank to preserve NOT NULL fields that are NOT in form
                        var existingTank = await _context.Tank.AsNoTracking().FirstOrDefaultAsync(t => t.Tank_Name == tank.Tank_Name);
                        if (existingTank != null)
                        {
                            // Preserve existing values ONLY for fields NOT in [Bind] list / NOT in form
                            if (string.IsNullOrEmpty(tank.Group_Name)) tank.Group_Name = existingTank.Group_Name ?? "";
                            if (string.IsNullOrEmpty(tank.Tank_Form)) tank.Tank_Form = existingTank.Tank_Form ?? "";

                            // Fields NOT in form - preserve from existing
                            tank.Height_Point_Desk = existingTank.Height_Point_Desk;
                            tank.Height_Tank_Base = existingTank.Height_Tank_Base;
                            tank.Stretch_Coefficient = existingTank.Stretch_Coefficient;
                            tank.Density_Calibrate = existingTank.Density_Calibrate;
                            tank.RaisePerMM = existingTank.RaisePerMM;

                            // Fields IN form - DO NOT override, use value from form binding
                            // - TankVolume (mapped to Height_Vol_Max) ← from form
                            // - Height_Deadstock ← from form
                            // - VolumeSafeCapacity (mapped to Height_Safe_Capacity) ← from form
                        }

                        tank.Update_By = User.Identity.Name;
                        tank.Update_Time = DateTime.Now;

                        // Update or Create Tank_LiveData
                        var existingLiveData = await _context.Tank_Live_Data.FirstOrDefaultAsync(t => t.Tank_Number == tank.Tank_Name);
                        if (existingLiveData != null)
                        {
                            // UPDATE existing Tank_LiveData - sync Product_ID with Tank
                            existingLiveData.Product_ID = tank.Product_ID;
                            _context.Update(existingLiveData);
                            Console.WriteLine($"Updating Tank_LiveData for {tank.Tank_Name} - Product_ID: {tank.Product_ID}");
                        }
                        else
                        {
                            // CREATE new Tank_LiveData if doesn't exist
                            var newLiveData = new TankLiveData
                            {
                                Tank_Number = tank.Tank_Name,
                                Product_ID = tank.Product_ID
                            };
                            _context.Add(newLiveData);
                            Console.WriteLine($"Creating Tank_LiveData for {tank.Tank_Name} - Product_ID: {tank.Product_ID}");
                        }

                        _context.Update(tank);
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!TankExists(tank.Tank_Name))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Tank.ToList()) });
            }
            catch (Exception ex)
            {
                // Log error untuk debugging
                Console.WriteLine($"Tank AddOrEdit Error: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Return error message to client for debugging
                return Json(new {
                    isValid = false,
                    html = Helper.RenderRazorViewString(this, "AddOrEdit", tank),
                    errorMessage = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }

        }


        // POST: Tanks/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            // ✅ AUTHORIZATION: Only admins can delete
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            // CASCADE DELETE: First delete Tank_LiveData, then Tank
            // This prevents orphaned records in Tank_LiveData
            var tankLiveData = await _context.Tank_Live_Data.FirstOrDefaultAsync(t => t.Tank_Number == id);
            if (tankLiveData != null)
            {
                _context.Tank_Live_Data.Remove(tankLiveData);
                Console.WriteLine($"Deleting Tank_LiveData for {id}");
            }

            var tank = await _context.Tank.FindAsync(id);
            if (tank != null)
            {
                _context.Tank.Remove(tank);
                Console.WriteLine($"Deleting Tank {id}");
            }

            await _context.SaveChangesAsync();

            return Json(new { html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Tank.ToList()) });
        }

        private bool TankExists(string tankName)
        {
          return _context.Tank.Any(e => e.Tank_Name == tankName);
        }
        private void populateProductSection(object selectedSection = null)
        {
            List<Product> products = new List<Product>();
            products = (from p in _context.Master_Product select p).ToList();
            var productip = new Product()
            {
                Product_ID = Guid.Empty,
                Product_Name = "--- Select Product ---"
            };
            products.Insert(0, productip);
            ViewBag.ProductId = products;
        }
    }
}
