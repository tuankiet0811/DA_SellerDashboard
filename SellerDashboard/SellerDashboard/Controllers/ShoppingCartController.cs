using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SellerDashboard.Data;
using SellerDashboard.Extensions;
using SellerDashboard.Models;

namespace SellerDashboard.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private const string CartSessionKey = "Cart";

        public ShoppingCartController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<List<CartItem>> GetCartItemsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Promotion)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart == null) return new List<CartItem>();

                return cart.Items.Select(i => new CartItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Price = i.Product.DiscountedPrice,
                    Quantity = i.Quantity,
                    ImageUrl = i.Product.ImageUrl
                }).ToList();
            }

            return HttpContext.Session.Get<List<CartItem>>(CartSessionKey) ?? new List<CartItem>();
        }

        private async Task SaveCartItemAsync(int productId, int quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart == null)
                {
                    cart = new ShoppingCart { UserId = user.Id };
                    _context.ShoppingCarts.Add(cart);
                }

                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    cart.Items.Add(new ShoppingCartItem
                    {
                        ProductId = productId,
                        Quantity = quantity
                    });
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                var cart = HttpContext.Session.Get<List<CartItem>>(CartSessionKey) ?? new List<CartItem>();
                var product = _context.Products.Include(p => p.Promotion).FirstOrDefault(p => p.Id == productId);
                if (product != null)
                {
                    var existingItem = cart.FirstOrDefault(i => i.ProductId == productId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += quantity;
                        existingItem.Price = product.DiscountedPrice;
                    }
                    else
                    {
                        cart.Add(new CartItem
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            Price = product.DiscountedPrice,
                            Quantity = quantity,
                            ImageUrl = product.ImageUrl
                        });
                    }
                    HttpContext.Session.Set(CartSessionKey, cart);
                }
            }
        }

        private async Task RemoveCartItemAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart != null)
                {
                    var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                    if (item != null)
                    {
                        cart.Items.Remove(item);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var cart = HttpContext.Session.Get<List<CartItem>>(CartSessionKey) ?? new List<CartItem>();
                var item = cart.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    cart.Remove(item);
                    HttpContext.Session.Set(CartSessionKey, cart);
                }
            }
        }

        private async Task UpdateCartItemQuantityAsync(int productId, int quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart != null)
                {
                    var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                    if (item != null)
                    {
                        if (quantity > 0)
                        {
                            item.Quantity = quantity;
                        }
                        else
                        {
                            cart.Items.Remove(item);
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var cart = HttpContext.Session.Get<List<CartItem>>(CartSessionKey) ?? new List<CartItem>();
                var item = cart.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    if (quantity > 0)
                    {
                        item.Quantity = quantity;
                    }
                    else
                    {
                        cart.Remove(item);
                    }
                    HttpContext.Session.Set(CartSessionKey, cart);
                }
            }
        }

        private async Task ClearCartAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (cart != null)
                {
                    _context.ShoppingCarts.Remove(cart);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                HttpContext.Session.Remove(CartSessionKey);
            }
        }

        // GET: /ShoppingCart/Cart
        public async Task<IActionResult> Cart()
        {
            var cart = await GetCartItemsAsync();
            return View(cart);
        }

        // GET: /ShoppingCart/Checkout
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetCartItemsAsync();
            
            if (!cart.Any())
            {
                return RedirectToAction("Cart");
            }

            var viewModel = new CheckoutViewModel
            {
                CartItems = cart,
                CartTotal = cart.Sum(item => item.Total)
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            await SaveCartItemAsync(productId, quantity);
            var cart = await GetCartItemsAsync();
            return Json(new { success = true, count = cart.Sum(i => i.Quantity), message = "Product added to cart" });
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var cart = await GetCartItemsAsync();
            return Json(new { count = cart.Sum(i => i.Quantity) });
        }
        
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            await RemoveCartItemAsync(productId);
            return RedirectToAction("Cart");
        }
        
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            await UpdateCartItemQuantityAsync(productId, quantity);
            return RedirectToAction("Cart");
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var cart = await GetCartItemsAsync();
            
            if (!cart.Any())
            {
                return RedirectToAction("Cart");
            }

            // Re-populate cart data for the view if validation fails
            model.CartItems = cart;
            model.CartTotal = cart.Sum(item => item.Total);

            if (ModelState.IsValid)
            {
                var order = new Order
                {
                    OrderDate = DateTime.Now,
                    TotalAmount = model.CartTotal,
                    Status = "Pending",
                    Email = (await _userManager.GetUserAsync(User))?.Email ?? model.Email,
                    Phone = model.Phone,
                    FullName = model.FullName,
                    Province = model.Province,
                    District = model.District,
                    Ward = model.Ward,
                    SpecificAddress = model.SpecificAddress
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cart)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Price = item.Price,
                        Quantity = item.Quantity
                    };
                    _context.OrderDetails.Add(orderDetail);

                    // Update Stock
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity -= item.Quantity;
                    }
                }

                await _context.SaveChangesAsync();

                // Clear Cart
                await ClearCartAsync();

                return RedirectToAction("Success", new { orderId = order.Id });
            }

            return View("Checkout", model);
        }

        public IActionResult Success(int orderId)
        {
            return View(orderId); // Make sure Success view expects int model
        }
    }
}
