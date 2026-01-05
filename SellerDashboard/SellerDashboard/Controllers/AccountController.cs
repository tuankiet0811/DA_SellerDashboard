using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SellerDashboard.Data;
using SellerDashboard.Extensions;
using SellerDashboard.Models;
using SellerDashboard.ViewModels;

namespace SellerDashboard.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        private async Task MergeSessionCartToDbAsync(IdentityUser user)
        {
            var sessionCart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (sessionCart != null && sessionCart.Any())
            {
                var dbCart = await _context.ShoppingCarts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (dbCart == null)
                {
                    dbCart = new ShoppingCart { UserId = user.Id };
                    _context.ShoppingCarts.Add(dbCart);
                }

                foreach (var sessionItem in sessionCart)
                {
                    var existingItem = dbCart.Items.FirstOrDefault(i => i.ProductId == sessionItem.ProductId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += sessionItem.Quantity;
                    }
                    else
                    {
                        dbCart.Items.Add(new ShoppingCartItem
                        {
                            ProductId = sessionItem.ProductId,
                            Quantity = sessionItem.Quantity
                        });
                    }
                }

                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Cart");
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Tự động gán role Customer
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    }
                    
                    await _userManager.AddToRoleAsync(user, "Customer");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    await MergeSessionCartToDbAsync(user);

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null) 
                    {
                        await MergeSessionCartToDbAsync(user);
                        
                        if (await _userManager.IsInRoleAsync(user, "Admin"))
                        {
                            return RedirectToAction("Dashboard", "Admin");
                        }
                    }
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Đăng nhập không thành công.");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
