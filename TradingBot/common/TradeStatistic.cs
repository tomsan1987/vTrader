using System;
using System.Collections.Generic;
using Tinkoff.Trading.OpenApi.Models;

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
        public Dictionary<string, decimal> volumes = new Dictionary<string, decimal>();
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
        }

        public void Sell(DateTime buyTime, DateTime sellTime, decimal price)
        {
            do
            {
                var key = buyTime.ToString("yyyy-MM-dd-HH-mm");
                if (volumes.ContainsKey(key))
                    volumes[key] += price;
                else
                    volumes.Add(key, price);

                buyTime = buyTime.AddMinutes(5);
            }
            while (buyTime <= sellTime);
        }

        public decimal GetMaxVolume()
        {
            decimal max = 0;
            foreach (var it in volumes)
                max = Math.Max(max, it.Value);

            return max;
        }
    }
}
