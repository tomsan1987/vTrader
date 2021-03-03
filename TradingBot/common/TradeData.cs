using System;

namespace TradingBot
{
    public class TradeData
    {
        public string OrderId { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal MaxPrice { get; set; }
        public Status Status { get; set; }
        public DateTime Time { get; set; }
        public DateTime BuyTime { get; set; }
        public IStrategy Strategy { get; set; }
        public IStrategyData StrategyData { get; set; }
        public Trend Trend { get; set; }
        public bool DisabledTrading { get; set; } = false;

        public TradeData()
        {
            Status = Status.Watching;
            Time = DateTime.Today.AddYears(-10).ToUniversalTime();
        }

        public void Reset(bool full)
        {
            OrderId = "";
            BuyPrice = 0;
            SellPrice = 0;
            StopLoss = 0;
            TakeProfit = 0;
            MaxPrice = 0;
            Status = Status.Watching;
            BuyTime = DateTime.Today.AddYears(-10).ToUniversalTime();
            Strategy = null;
            StrategyData = null;
            Trend = null;
            DisabledTrading = false;

            if (full)
                Time = DateTime.Today.AddYears(-10).ToUniversalTime();
        }
    }

    public interface IStrategyData
    {
    }
}
