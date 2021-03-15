using System;
using System.Collections.Generic;

namespace TradingBot
{
    public class TradeData
    {
        public decimal AvgPrice { get; set; }
        public int Lots { get; set; }
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
        public List<Position> Positions { get; set; }

        public TradeData()
        {
            Status = Status.Watching;
            Time = DateTime.Today.AddYears(-10).ToUniversalTime();
            Positions = new List<Position>();
        }

        public void Reset(bool full)
        {
            AvgPrice = 0;
            Lots = 0;
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
            Positions.Clear();

            if (full)
                Time = DateTime.Today.AddYears(-10).ToUniversalTime();
        }

        public void OnBuy(int lots, decimal price)
        {
            Positions.Add(new Position(lots, price));

            CalculateAverage();

            Status = Status.BuyDone;
        }

        public void OnSell(int lots, decimal price)
        {
            if (Lots < lots)
            {
                Logger.Write("ERROR! Something wrong. Sold more lots than expected. Trading disabled! Current lots: {0}. Sell lots: {1}", Lots, lots);
                DisabledTrading = true;
                return;
            }

            while (lots > 0)
            {
                var first = Positions[0];
                if (first.Lots < lots)
                {
                    // just update
                    first.Lots -= lots;
                    lots = 0;
                }
                else
                {
                    // remove first element and continue
                    Positions.Remove(first);
                    lots -= first.Lots;
                }
            }

            CalculateAverage();

            Status = Status.SellDone;
        }

        private void CalculateAverage()
        {
            // calculate average price
            int totalLots = 0;
            decimal totalPrice = 0;
            foreach (var it in Positions)
            {
                totalLots += it.Lots;
                totalPrice += it.Lots * it.Price;
            }

            AvgPrice = 0;
            Lots = totalLots;
            if (totalLots > 0)
                AvgPrice = totalPrice / totalLots;
        }
    }

    public class Position
    {
        public Position(int l, decimal p)
        {
            Lots = l;
            Price = p;
        }

        public int Lots { get; set; }
        public decimal Price { get; set; }

    }
    public interface IStrategyData
    {
    }
}
