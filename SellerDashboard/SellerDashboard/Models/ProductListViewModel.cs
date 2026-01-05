using SellerDashboard.Models;

namespace SellerDashboard.Models
{
    public class ProductListViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public Dictionary<string, int> Categories { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Brands { get; set; } = new Dictionary<string, int>();
        
        // Filter states
        public string? CurrentCategory { get; set; }
        public string? CurrentBrand { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortOrder { get; set; }
        public string? SearchTerm { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }
}