using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    class TradeData
    {
        public string OrderId { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal StopPrice { get; set; }
        public Status Status { get; set; }
        public TradeStatistic Stats { get; set; }

        public TradeData()
        {
            Status = Status.Watching;
        }
    }
}
