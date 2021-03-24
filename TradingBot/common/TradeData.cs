﻿using System;
using System.Collections.Generic;
using Tinkoff.Trading.OpenApi.Models;

namespace TradingBot
{
    public class TradeData
    {
        public decimal AvgPrice { get; set; }
        public decimal AvgSellPrice { get; set; }
        public int Lots { get; set; }
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
        public List<Position> Positions { get; set; } = new List<Position>();

        public TradeData()
        {
            Reset(true);
        }

        public void Reset(bool full)
        {
            AvgPrice = 0;
            AvgSellPrice = 0;
            Lots = 0;
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

        public void Update(OperationType type, int lots, decimal price, string orderID)
        {
            if (type == OperationType.Buy)
                OnBuy(lots, price, orderID);
            else
                OnSell(lots, price, orderID);
        }

        public int GetOrdersInLastTrade()
        {
            int totalOrders = 0;
            string prevOrderID = "";
            for (int i = GetLastTradeBeginPositions(); i < Positions.Count; ++i)
            {
                if (Positions[i].Lots > 0)
                {
                    if (prevOrderID != Positions[i].OrderID)
                    {
                        ++totalOrders;
                        prevOrderID = Positions[i].OrderID;
                    }
                }
            }

            return totalOrders;
        }
        public int GetLotsInLastTrade()
        {
            int totalLots = 0;
            for (int i = GetLastTradeBeginPositions(); i < Positions.Count; ++i)
            {
                if (Positions[i].Lots > 0)
                {
                    totalLots += Positions[i].Lots;
                }
            }

            return totalLots;
        }

        private void OnBuy(int lots, decimal price, string orderID)
        {
            Positions.Add(new Position(lots, price, orderID));

            CalculateAverage();

            Status = Status.BuyDone;
        }

        private void OnSell(int lots, decimal price, string orderID)
        {
            if (Lots < lots)
            {
                Logger.Write("ERROR! Something wrong. Sold more lots than expected. Trading disabled! Current lots: {0}. Sell lots: {1}", Lots, lots);
                DisabledTrading = true;
                return;
            }

            Positions.Add(new Position(-lots, price, orderID));

            CalculateAverage();

            if (Lots == 0)
                Status = Status.SellDone;
            else
                Status = Status.BuyDone;
        }

        private void CalculateAverage()
        {
            // calculate average price
            int totalBuyLots = 0;
            decimal totalBuyPrice = 0;
            int totalSellLots = 0;
            decimal totalSellPrice = 0;

            for (int i = GetLastTradeBeginPositions(); i < Positions.Count; ++i)
            {
                if (Positions[i].Lots > 0)
                {
                    totalBuyLots += Positions[i].Lots;
                    totalBuyPrice += Positions[i].Lots * Positions[i].Price;
                }
                else
                {
                    totalSellLots += -Positions[i].Lots;
                    totalSellPrice += -Positions[i].Lots * Positions[i].Price;
                }
            }

            AvgPrice = 0;
            AvgSellPrice = 0;
            Lots = totalBuyLots - totalSellLots;

            if (totalBuyLots > 0)
                AvgPrice = totalBuyPrice / totalBuyLots;

            if (totalSellLots > 0)
                AvgSellPrice = totalSellPrice / totalSellLots;
        }

        private int GetLastTradeBeginPositions()
        {
            // get last index where lots count == 0
            int totalLots = 0;
            int posBegin = 0;
            for (int i = 0; i < Positions.Count; ++i)
            {
                totalLots += Positions[i].Lots;
                if (totalLots == 0)
                    posBegin = i + 1;
            }

            if (posBegin == Positions.Count)
                posBegin = 0;

            return posBegin;
        }
    }


    public class Position
    {
        public Position(int l, decimal p, string ID)
        {
            Lots = l;
            Price = p;
            OrderID = ID;
        }

        public int Lots { get; set; }
        public decimal Price { get; set; }
        public string OrderID { get; set; }
    }
    public interface IStrategyData
    {
    }
}
