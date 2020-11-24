using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    class TradeStatistic
    {
        public decimal totalProfit = 0;
        public int totalOrders = 0;
        public int posOrders = 0;
        public int negOrders = 0;

        public void Update(decimal buyPrice, decimal sellPrice)
        {
            totalProfit += sellPrice - buyPrice;
            totalOrders++;
            if (sellPrice >= buyPrice)
                posOrders++;
            else
                negOrders++;
        }
    }
}
