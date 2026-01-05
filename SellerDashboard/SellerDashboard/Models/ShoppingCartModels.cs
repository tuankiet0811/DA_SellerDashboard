using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace SellerDashboard.Models
{
    public class ShoppingCart
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; } // Can be null for guest carts if we decide to store them in DB later, but for now we focus on logged in users.

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<ShoppingCartItem> Items { get; set; } = new List<ShoppingCartItem>();
    }

    public class ShoppingCartItem
    {
        [Key]
        public int Id { get; set; }

        public int ShoppingCartId { get; set; }
        [ForeignKey("ShoppingCartId")]
        public ShoppingCart ShoppingCart { get; set; } = null!;

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
    }
}
