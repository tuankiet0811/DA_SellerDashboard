using System;
using System.Collections.Generic;
using SellerDashboard.Models;

namespace SellerDashboard.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }

        public int CancelledCount { get; set; }
        public int ReturnRequestedCount { get; set; }
        public int ReturnedCount { get; set; }
        public decimal RefundedAmount { get; set; }

        public double ConversionRatePercent { get; set; }

        public int TotalSkus { get; set; }
        public int TotalStockUnits { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockCount { get; set; }
        public List<Product> LowStockProducts { get; set; } = new List<Product>();
    }
}