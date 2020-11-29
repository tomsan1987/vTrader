using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    class TradeStatistic
    {
        public decimal volume = 0;
        public decimal comission = 0;
        public decimal totalProfit = 0;
        public int totalOrders = 0;
        public int posOrders = 0;
        public int negOrders = 0;
        public decimal currVolume = 0;
        public decimal maxVolume = 0;
        public List<string> logMessages = new List<string>();

        public void Update(string ticker, decimal buyPrice, decimal sellPrice)
        {
            totalProfit += sellPrice - buyPrice;
            totalOrders++;
            if (sellPrice >= buyPrice)
                posOrders++;
            else
                negOrders++;

            // comission
            comission += buyPrice * (decimal)0.0005;
            comission += sellPrice * (decimal)0.0005;

            volume += buyPrice;

            logMessages.Add(String.Format("{0};{1};{2}", ticker, sellPrice - buyPrice, Helpers.GetChangeInPercent(buyPrice, sellPrice)));
        }

        public void Buy(decimal price)
        {
            currVolume += price;
            if (currVolume > maxVolume)
                maxVolume = currVolume;
        }

        public void Sell(decimal price)
        {
            currVolume -= price;
        }
    }
}
