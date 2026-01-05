using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SellerDashboard.Data;
using SellerDashboard.Models;

namespace SellerDashboard.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Dashboard()
        {
            var orders = await _context.Orders.ToListAsync();
            var products = await _context.Products.ToListAsync();

            var completedOrders = orders.Where(o => (o.Status == "Completed" || o.Status == "Delivered") && o.PaymentStatus != "Refunded");
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);

            var totalOrders = completedOrders.Count();
            var averageOrderValue = totalOrders > 0 ? completedOrders.Average(o => o.TotalAmount) : 0m;

            var cancelled = orders.Count(o => o.Status == "Cancelled");
            var returnRequested = orders.Count(o => o.Status == "ReturnRequested");
            var returned = orders.Count(o => o.Status == "Returned");
            var refundedAmount = orders.Where(o => o.PaymentStatus == "Refunded").Sum(o => o.TotalAmount);

            var convertedCount = orders.Count(o => o.Status == "Completed" || o.Status == "Delivered");
            var totalAllOrders = orders.Count;
            double conversionRate = totalAllOrders > 0 ? Math.Round(convertedCount * 100.0 / totalAllOrders, 1) : 0.0;

            var vm = new SellerDashboard.ViewModels.AdminDashboardViewModel
            {
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                AverageOrderValue = averageOrderValue,
                CancelledCount = cancelled,
                ReturnRequestedCount = returnRequested,
                ReturnedCount = returned,
                RefundedAmount = refundedAmount,
                ConversionRatePercent = conversionRate,
                TotalSkus = products.Count,
                TotalStockUnits = products.Sum(p => p.StockQuantity),
                OutOfStockCount = products.Count(p => p.StockQuantity <= 0),
                LowStockCount = products.Count(p => p.StockQuantity > 0 && p.StockQuantity <= 5),
                LowStockProducts = products.Where(p => p.StockQuantity > 0 && p.StockQuantity <= 5).OrderBy(p => p.StockQuantity).Take(10).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStock(int id, int quantity)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "Product not found." });
            }
            if (quantity < 0)
            {
                quantity = 0;
            }
            product.StockQuantity = quantity;
            _context.Update(product);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData(DateTime? from, DateTime? to, string granularity = "daily")
        {
            DateTime today = DateTime.Today;
            DateTime start = from ?? today;
            DateTime end = to ?? today.AddDays(1);

            var orders = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            var completedOrders = orders.Where(o => (o.Status == "Completed" || o.Status == "Delivered") && o.PaymentStatus != "Refunded");
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);

            var totalOrders = completedOrders.Count();
            var averageOrderValue = totalOrders > 0 ? completedOrders.Average(o => o.TotalAmount) : 0m;

            var cancelled = orders.Count(o => o.Status == "Cancelled");
            var returnRequested = orders.Count(o => o.Status == "ReturnRequested");
            var returned = orders.Count(o => o.Status == "Returned");
            var refundedAmount = orders.Where(o => o.PaymentStatus == "Refunded").Sum(o => o.TotalAmount);

            var convertedCount = orders.Count(o => o.Status == "Completed" || o.Status == "Delivered");
            var totalAllOrders = orders.Count;
            double conversionRate = totalAllOrders > 0 ? Math.Round(convertedCount * 100.0 / totalAllOrders, 1) : 0.0;

            IEnumerable<object> series;
            if (granularity == "monthly")
            {
                series = completedOrders
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new { date = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM-dd"), amount = g.Sum(x => x.TotalAmount) })
                    .OrderBy(x => x.date)
                    .ToList();
            }
            else
            {
                series = completedOrders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), amount = g.Sum(x => x.TotalAmount) })
                    .OrderBy(x => x.date)
                    .ToList();
            }

            var orderIds = completedOrders.Select(o => o.Id).ToList();
            var completedDetails = await _context.OrderDetails
                .Where(od => orderIds.Contains(od.OrderId))
                .Include(od => od.Product)
                .ToListAsync();

            var categoryBreakdown = completedDetails
                .GroupBy(od => string.IsNullOrEmpty(od.Product?.Category) ? "Không phân loại" : od.Product!.Category!)
                .Select(g => new { name = g.Key, amount = g.Sum(x => x.Price * x.Quantity) })
                .OrderByDescending(x => x.amount)
                .ToList();

            var totalCatAmount = categoryBreakdown.Sum(c => c.amount);
            var categories = categoryBreakdown.Select(c => new { name = c.name, percent = totalCatAmount > 0 ? Math.Round((double)(c.amount / totalCatAmount) * 100.0, 1) : 0.0 }).ToList();
            var topCategory = categories.FirstOrDefault() ?? new { name = "N/A", percent = 0.0 };

            var completedCountNonRefunded = completedOrders.Count();
            var returnPieTotal = completedCountNonRefunded + returned + returnRequested + cancelled;
            var returnPie = new[]
            {
                new { name = "Returned", percent = returnPieTotal > 0 ? Math.Round(returned * 100.0 / returnPieTotal, 1) : 0.0, count = returned },
                new { name = "Return Requested", percent = returnPieTotal > 0 ? Math.Round(returnRequested * 100.0 / returnPieTotal, 1) : 0.0, count = returnRequested },
                new { name = "Cancelled", percent = returnPieTotal > 0 ? Math.Round(cancelled * 100.0 / returnPieTotal, 1) : 0.0, count = cancelled },
                new { name = "Completed", percent = returnPieTotal > 0 ? Math.Round(completedCountNonRefunded * 100.0 / returnPieTotal, 1) : 0.0, count = completedCountNonRefunded }
            };

            var regionBreakdown = completedOrders
                .GroupBy(o => string.IsNullOrEmpty(o.Province) ? "Không rõ" : o.Province!)
                .Select(g => new { name = g.Key, amount = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(x => x.amount)
                .ToList();

            var totalRegionAmount = regionBreakdown.Sum(r => r.amount);
            var regions = regionBreakdown.Select(r => new { name = r.name, percent = totalRegionAmount > 0 ? Math.Round((double)(r.amount / totalRegionAmount) * 100.0, 1) : 0.0, amount = r.amount }).ToList();

            return Json(new
            {
                totalRevenue,
                totalOrders,
                averageOrderValue,
                cancelled,
                returnRequested,
                returned,
                refundedAmount,
                conversionRate,
                series,
                categories,
                topCategory,
                returnPie,
                regions
            });
        }

        // --- Product Management ---

        [HttpGet]
        public async Task<IActionResult> ProductList()
        {
            var products = await _context.Products.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return PartialView("_ProductList", products);
        }

        [HttpGet]
        public IActionResult CreateProduct()
        {
            return PartialView("_CreateProduct", new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Tạo sản phẩm thành công!" });
            }
            // If invalid, return the partial view with validation errors
            return PartialView("_CreateProduct", product);
        }

        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return PartialView("_EditProduct", product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product, IFormFile? imageFile)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if exists and not default
                        if (!string.IsNullOrEmpty(product.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        product.ImageUrl = "/images/products/" + uniqueFileName;
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Cập nhật sản phẩm thành công!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return PartialView("_EditProduct", product);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa sản phẩm thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
        }

        // --- Order Management ---

        [HttpGet]
        public async Task<IActionResult> OrderList()
        {
            var orders = await _context.Orders.OrderByDescending(o => o.OrderDate).ToListAsync();
            return PartialView("_OrderList", orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            if (order.PaymentStatus == "Refunded")
            {
                return Json(new { success = false, message = "Không thể cập nhật trạng thái đơn hàng đã hoàn tiền." });
            }

            order.Status = status;

            // If order is completed/delivered, update payment status to Paid
            if (status == "Completed" || status == "Delivered")
            {
                order.PaymentStatus = "Paid";
            }
            else if (status == "Returned")
            {
                order.PaymentStatus = "Refunded";
            }

            _context.Update(order);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật trạng thái đơn hàng thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> ApproveReturn(int id, bool approve)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            if (order.Status != "ReturnRequested")
            {
                return Json(new { success = false, message = "Đơn hàng không ở trạng thái yêu cầu trả hàng." });
            }

            if (approve)
            {
                order.Status = "Returned";
                order.PaymentStatus = "Refunded";
            }
            else
            {
                order.Status = "Completed";
                order.PaymentStatus = "Paid";
            }

            _context.Update(order);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = approve ? "Đã duyệt trả hàng." : "Đã từ chối trả hàng." });
        }

        // --- Promotion Management ---

        [HttpGet]
        public async Task<IActionResult> PromotionList()
        {
            var promotions = await _context.Promotions.OrderByDescending(p => p.StartDate).ToListAsync();
            return PartialView("_PromotionList", promotions);
        }

        [HttpGet]
        public IActionResult CreatePromotion()
        {
            return PartialView("_CreatePromotion", new Promotion { StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(7) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePromotion(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                _context.Add(promotion);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Tạo khuyến mãi thành công!" });
            }
            return PartialView("_CreatePromotion", promotion);
        }

        [HttpGet]
        public async Task<IActionResult> EditPromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }
            return PartialView("_EditPromotion", promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPromotion(int id, Promotion promotion)
        {
            if (id != promotion.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(promotion);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Cập nhật khuyến mãi thành công!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Promotions.Any(e => e.Id == promotion.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return PartialView("_EditPromotion", promotion);
        }

        [HttpPost]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _context.Promotions.Include(p => p.Products).FirstOrDefaultAsync(p => p.Id == id);
            if (promotion != null)
            {
                // Reset products associated with this promotion
                foreach(var product in promotion.Products)
                {
                    product.PromotionId = null;
                }
                
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa khuyến mãi thành công!" });
            }
            return Json(new { success = false, message = "Không tìm thấy khuyến mãi." });
        }

        [HttpGet]
        public async Task<IActionResult> ApplyPromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }
            var products = await _context.Products.Include(p => p.Promotion).ToListAsync();
            ViewBag.PromotionId = id;
            ViewBag.PromotionName = promotion.Name;
            return PartialView("_ApplyPromotion", products);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyPromotionToProducts(int promotionId, List<int> productIds)
        {
            var promotion = await _context.Promotions.FindAsync(promotionId);
            if (promotion == null)
            {
                return Json(new { success = false, message = "Không tìm thấy khuyến mãi." });
            }

            // Get all products that currently have this promotion
            var existingProducts = await _context.Products.Where(p => p.PromotionId == promotionId).ToListAsync();
            
            // Clear promotion from products not in the new list
            foreach (var p in existingProducts)
            {
                if (!productIds.Contains(p.Id))
                {
                    p.PromotionId = null;
                }
            }

            // Add promotion to selected products
            var productsToUpdate = await _context.Products.Include(p => p.Promotion).Where(p => productIds.Contains(p.Id)).ToListAsync();
            foreach (var p in productsToUpdate)
            {
                // Prevent overwriting if product is in another active promotion
                if (p.PromotionId != null && p.PromotionId != promotionId)
                {
                     if (p.Promotion != null && p.Promotion.EndDate >= DateTime.Now)
                     {
                         continue; // Skip this product
                     }
                }
                p.PromotionId = promotionId;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Áp dụng khuyến mãi thành công!" });
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
