using System;

namespace MyFirstCRM
{
    public class TopClientViewModel
    {
        public string Name { get; set; }
        public int DealCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Profit { get; set; }
        public double Rating { get; set; }
        public int Rank { get; set; }
    }

    public class ActiveDealViewModel
    {
        public string Title { get; set; }
        public string Client { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public DateTime? Deadline { get; set; }
    }
}