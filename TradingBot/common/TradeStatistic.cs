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
        public Dictionary<string, bool[]> volumesPerTicker = new Dictionary<string, bool[]>();
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
                    volumesPerTicker.Add(ticker, new bool[12 * 17]);
                volumesPerTicker[ticker][(buyTime.Hour - 7) * 12 + buyTime.Minute / 5] = true;

                buyTime = buyTime.AddMinutes(5);
            }
            while (buyTime <= sellTime);
        }

        public string GetVolumeDistribution()
        {
            string result = "\nTicker;";
            DateTime start = DateTime.Today.AddHours(10).ToUniversalTime();
            for (int i = 0; i < 12 * 17; ++i)
            {
                result += start.ToString("HH-mm");
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
            decimal max = 0;
            foreach (var it in volumes)
                max = Math.Max(max, it.Value);

            return max;
        }
    }
}
