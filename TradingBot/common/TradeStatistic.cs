using System;
using System.Collections.Generic;
using Tinkoff.Trading.OpenApi.Models;

namespace TradingBot
{
    public class TradeStatistic : ICloneable
    {
        public decimal volume = 0;
        public decimal maxVolume = 0;
        public decimal comission = 0;
        public decimal totalProfit = 0;
        public int totalOrders = 0;
        public int posOrders = 0;
        public int negOrders = 0;
        public int lots = 0;
        public Dictionary<string, decimal> volumes = new Dictionary<string, decimal>();
        public Dictionary<string, bool[]> volumesPerTicker = new Dictionary<string, bool[]>();
        public List<string> logMessages = new List<string>();

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void Update(string ticker, decimal buyPrice, decimal sellPrice, int orders, int l)
        {
            totalProfit += Math.Round((sellPrice - buyPrice) * l, 2);
            totalOrders += orders;
            if (sellPrice >= buyPrice)
                posOrders++;
            else
                negOrders++;

            lots += l;

            decimal k = volume * 2 > 2740 ? 0.00025m : 0.0005m; // when volume > 200k RUB, commission is reduced to 0.025%

            // commission
            comission += buyPrice * l * k;
            comission += sellPrice * l *k;

            logMessages.Add(String.Format("{0};{1};{2};{3};{4}", ticker, orders, l, (sellPrice - buyPrice) * l, Helpers.GetChangeInPercent(buyPrice, sellPrice)));
        }

        public void Sell(DateTime buyTime, DateTime sellTime, decimal price, string ticker)
        {
            do
            {
                // update volume
                var key = buyTime.ToString("yyyy-MM-dd-HH-mm");
                if (volumes.ContainsKey(key))
                    volumes[key] += price;
                else
                    volumes.Add(key, price);

                // update duration
                if (!volumesPerTicker.ContainsKey(ticker))
                    volumesPerTicker.Add(ticker, new bool[12 * 20]);
                volumesPerTicker[ticker][(buyTime.Hour - 4) * 12 + buyTime.Minute / 5] = true;

                buyTime = buyTime.AddMinutes(5);
            }
            while (buyTime <= sellTime);
        }

        public string GetVolumeDistribution()
        {
            string result = "Ticker;";
            DateTime start = DateTime.Today.AddHours(10).ToUniversalTime();
            for (int i = 0; i < 12 * 20; ++i)
            {
                result += start.ToString("HH:mm");
                result += ";";
                start = start.AddMinutes(5);
            }
            result += "\n";

            foreach (var it in volumesPerTicker)
            {
                result += it.Key;
                result += ";";
                foreach (var x in it.Value)
                {
                    if (x)
                        result += "x";
                    result += ";";
                }
                result += "\n";
            }

            return result;
        }

        public decimal GetMaxVolume()
        {
            decimal max = maxVolume;

            if (volumes.Count > 0)
            {
                foreach (var it in volumes)
                    max = Math.Max(max, it.Value);
            }

            return max;
        }

        public string GetStringStat()
        {
            decimal ordersRatio = 0.0m;
            if (negOrders > 0)
                ordersRatio = Math.Round((decimal)posOrders / negOrders, 2);

            decimal profitPerOrder = 0.0m;
            if (totalOrders > 0)
                profitPerOrder = Math.Round(totalProfit / totalOrders, 2);

            return String.Format("Trade statistic. Total/Pos/Neg/(ratio): {0}/{1}/{2}/({3}); Profit: {4}/{5}; Volume: {6}; MaxVolume: {7}; Commission: {8};", totalOrders, posOrders, negOrders, ordersRatio, totalProfit, profitPerOrder, volume, GetMaxVolume(), comission);
        }
    }
}
