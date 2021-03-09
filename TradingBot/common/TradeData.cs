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
        public decimal PrevDayClosePrice { get; set; } // -1 - not calculated ; 0 - undefined; > 0 - close price of previous day
        public Status Status { get; set; }
        public DateTime Time { get; set; }
        public DateTime BuyTime { get; set; }
        public IStrategy Strategy { get; set; }
        public IStrategyData StrategyData { get; set; }
        public Trend Trend { get; set; }
        public bool DisabledTrading { get; set; } = false;
        public int CandleID { get; set; } // ID of candle where last operation was done

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
            PrevDayClosePrice = -1;
            Status = Status.Watching;
            BuyTime = DateTime.Today.AddYears(-10).ToUniversalTime();
            Strategy = null;
            StrategyData = null;
            Trend = null;
            DisabledTrading = false;
            CandleID = 0;

            if (full)
                Time = DateTime.Today.AddYears(-10).ToUniversalTime();
        }
    }

    public interface IStrategyData
    {
    }
}
