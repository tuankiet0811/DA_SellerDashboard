using System.ComponentModel.DataAnnotations;

namespace SellerDashboard.Models
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        [Phone(ErrorMessage = "Invalid Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Province is required")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "District is required")]
        public string District { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ward is required")]
        public string Ward { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specific Address is required")]
        [Display(Name = "Specific Address")]
        public string SpecificAddress { get; set; } = string.Empty;

        public List<CartItem> CartItems { get; set; } = new List<CartItem>();
        public decimal CartTotal { get; set; }
    }
}
