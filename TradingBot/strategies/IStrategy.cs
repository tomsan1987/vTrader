﻿using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;

namespace TradingBot
{
    public interface IStrategy
    {
        public enum StrategyResultType
        {
            Buy,
            Sell,
            Hold,
            NoOp
        }
        public StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes);
        public string Description();
    }
}
