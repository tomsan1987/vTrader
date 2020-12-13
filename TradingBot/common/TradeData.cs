using System;

namespace TradingBot
{
    public class TradeData
    {
        public string OrderId { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal StopPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public Status Status { get; set; }
        public DateTime Time { get; set; }
        public DateTime BuyTime { get; set; }
        public IStrategy Strategy { get; set; }

        public TradeData()
        {
            Status = Status.Watching;
            Time = DateTime.Today.AddYears(-10).ToUniversalTime();
        }

        public void Reset()
        {
            OrderId = "";
            BuyPrice = 0;
            SellPrice = 0;
            StopPrice = 0;
            MaxPrice = 0;
            Status = Status.Watching;
        }
    }
}
