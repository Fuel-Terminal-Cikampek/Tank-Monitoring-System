using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TMS.Models;
using CSL.Web;
using Microsoft.AspNetCore.Authorization;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Authorization;

namespace TMS.Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly TMSContext _context;

        public ProductsController(TMSContext context)
        {
            _context = context;
        }

        // GET: Products
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

                // getting all data
                var product = (from p in _context.Master_Product select p);//LINQ
                //Sorting  
                if (!(string.IsNullOrEmpty(sortColumn) && string.IsNullOrEmpty(sortColumnDirection)))
                {
                    product = product.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                //Search  
                if (!string.IsNullOrEmpty(searchValue))
                {
                    product = product.Where(m => m.Product_Name.Contains(searchValue));
                }

                //total number of rows counts
                recordsTotal = product.Count();
                //Paging
                var productList = product.Skip(skip).Take(pageSize).ToList();

                // Map to ensure proper property names for JSON
                var data = productList.Select(p => new
                {
                    Product_ID = p.Product_ID,
                    Product_Code = p.Product_Code,
                    Product_Name = p.Product_Name,
                    HexColor = p.HexColor,
                    Default_Density = p.Default_Density,
                    Default_Temp = p.Default_Temp,
                    Create_Time = p.Create_Time,
                    Update_Time = p.Update_Time,
                    Create_By = p.Create_By,
                    Update_By = p.Update_By
                }).ToList();

                // Debug logging
                Console.WriteLine($"Product LoadData - Total records: {recordsTotal}");
                if (data.Count > 0)
                {
                    var firstItem = data.First();
                    Console.WriteLine($"Product LoadData - First item ID: {firstItem.Product_ID}");
                    Console.WriteLine($"Product LoadData - First item Code: {firstItem.Product_Code}");
                    Console.WriteLine($"Product LoadData - First item Name: {firstItem.Product_Name}");
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
        // GET: Products/AddOrEdit
        [NoDirectAccess]
        public async Task<IActionResult> AddOrEdit(string id = null)
        {
            if (string.IsNullOrEmpty(id) || id == "0" || !Guid.TryParse(id, out Guid guidId) || guidId == Guid.Empty)
            {
                // Insert mode
                return View(new Product());
            }
            else
            {
                // Edit mode
                var product = await _context.Master_Product.FindAsync(guidId);
                if (product == null)
                {
                    return NotFound();
                }
                return View(product);
            }
        }
       

        // POST: Products/AddOrEdit
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("Product_ID,Product_Code,Product_Name,HexColor,Default_Density,Default_Temp,Create_Time,Update_Time,Create_By,Update_By")] Product product)
        {
            if (ModelState.IsValid)
            {
                // Check if this is insert or update
                bool isInsert = product.Product_ID == Guid.Empty;

                if (isInsert)
                {
                    // Insert mode
                    product.Product_ID = Guid.NewGuid();
                    product.Create_By = User.Identity.Name;
                    product.Create_Time = DateTime.Now;
                    _context.Add(product);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Update mode
                    try
                    {
                        product.Update_By = User.Identity.Name;
                        product.Update_Time = DateTime.Now;
                        _context.Update(product);
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!ProductExists(product.Product_ID))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                return Json(new { isValid = true, html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Master_Product.ToList())});
            }
            return Json(new { isValid = false, html = Helper.RenderRazorViewString(this, "AddOrEdit", product) });
        }


        // POST: Products/Delete
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            // ✅ AUTHORIZATION: Only admins can delete
            if (!User.CanModifyData())
            {
                return this.AccessDeniedJson();
            }

            if (Guid.TryParse(id, out Guid guidId))
            {
                var product = await _context.Master_Product.FindAsync(guidId);
                if (product != null)
                {
                    _context.Master_Product.Remove(product);
                    await _context.SaveChangesAsync();
                }
            }
            return Json(new { html = Helper.RenderRazorViewString(this, "_ViewAll", _context.Master_Product.ToList()) });
        }

        private bool ProductExists(Guid id)
        {
          return _context.Master_Product.Any(e => e.Product_ID == id);
        }
    }
}
