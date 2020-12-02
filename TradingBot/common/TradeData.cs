using System;

namespace TradingBot
{
    class TradeData
    {
        public string OrderId { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal StopPrice { get; set; }
        public Status Status { get; set; }
        public DateTime Time { get; set; }

        public TradeData()
        {
            Status = Status.Watching;
        }

        public void Reset()
        {
            OrderId = "";
            BuyPrice = 0;
            SellPrice = 0;
            StopPrice = 0;
            Status = Status.Watching;
        }
    }
}
