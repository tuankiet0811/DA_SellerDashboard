using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellerDashboard.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [StringLength(200)]
        public string? ImageUrl { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        [StringLength(50)]
        public string? Brand { get; set; }

        public int StockQuantity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign Key for Promotion
        public int? PromotionId { get; set; }
        [ForeignKey("PromotionId")]
        public Promotion? Promotion { get; set; }

        // Helper to get discounted price
        [NotMapped]
        public decimal DiscountedPrice
        {
            get
            {
                if (Promotion != null && Promotion.IsActive && DateTime.Now >= Promotion.StartDate && DateTime.Now <= Promotion.EndDate)
                {
                    return Price * (1 - (decimal)Promotion.DiscountPercent / 100);
                }
                return Price;
            }
        }

        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
