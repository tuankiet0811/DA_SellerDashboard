using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SellerDashboard.Data;
using SellerDashboard.Models;

namespace SellerDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(string? category, string? brand, decimal? minPrice, decimal? maxPrice, string? sortOrder, string? searchTerm, int page = 1)
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            var query = _context.Products.Include(p => p.Promotion).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));
            }

            // Filters
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            if (!string.IsNullOrEmpty(brand))
            {
                query = query.Where(p => p.Brand == brand);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // Sorting
            query = sortOrder switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt) // Default
            };

            // Get Facets
            var categories = await _context.Products
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .GroupBy(p => p.Category)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Name!, v => v.Count);

            var brands = await _context.Products
                .Where(p => !string.IsNullOrEmpty(p.Brand))
                .GroupBy(p => p.Brand)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Name!, v => v.Count);

            // Pagination
            int pageSize = 9;
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var products = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var viewModel = new ProductListViewModel
            {
                Products = products,
                Categories = categories,
                Brands = brands,
                CurrentCategory = category,
                CurrentBrand = brand,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortOrder = sortOrder,
                SearchTerm = searchTerm,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Promotion)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string? comment)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError("rating", "Rating must be between 1 and 5");
            }

            var finalUserName = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finalUserName))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("Details", new { id = productId });
            }

            var existing = await _context.Reviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.UserName == finalUserName);
            if (existing != null)
            {
                existing.Rating = rating;
                existing.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
                _context.Reviews.Update(existing);
            }
            else
            {
                var review = new Review
                {
                    ProductId = productId,
                    UserName = finalUserName,
                    Rating = rating,
                    Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                    CreatedAt = DateTime.Now
                };
                _context.Reviews.Add(review);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = productId });
        }

        [HttpGet]
        public async Task<IActionResult> GetSearchSuggestions(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var products = await _context.Products
                .Where(p => p.Name.Contains(term))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.ImageUrl,
                    p.Price,
                    p.Category
                })
                .Take(5)
                .ToListAsync();

            return Json(products);
        }
    


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReview(int id, int rating, string? comment)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id);
            if (review == null)
            {
                return NotFound();
            }
            var userName = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userName) || !string.Equals(review.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError("rating", "Rating must be between 1 and 5");
            }
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Details", new { id = review.ProductId });
            }
            review.Rating = rating;
            review.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            _context.Reviews.Update(review);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = review.ProductId });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

