using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;

namespace TradingBot
{
    //
    // Summary:
    //     Stocks screener. Show day statistic for instruments.
    public class Screener : BaseBot
    {
        public class Stat : IComparable<Stat>
        {
            public string ticker;
            public decimal change;
            public decimal open;
            public decimal close;

            public Stat(string t, decimal o, decimal c)
            {
                ticker = t;
                open = o;
                close = c;
                change = Helpers.GetChangeInPercent(o, c);
            }

            public int CompareTo(Stat right)
            {
                if (this.change != right.change)
                    return (this.change < right.change) ? 1 : -1;

                return this.ticker.CompareTo(right.ticker);
            }
        }

        public Screener(Settings settings) : base(settings)
        {
            Logger.Write("Screener created");
        }

        public override void ShowStatus()
        {
            Console.Clear();
            ShowStatusStatic(_figiToTicker, _lastCandleReceived, _candles);
        }

        public static void ShowStatusStatic(Dictionary<string, string> figiToTicker, DateTime lastCandleReceived, Dictionary<string, Quotes> allCandles)
        {
            List<Stat> day1Change = new List<Stat>();
            List<Stat> hour1Change = new List<Stat>();
            List<Stat> min15Change = new List<Stat>();
            List<Stat> min5Change = new List<Stat>();

            foreach (var c in allCandles)
            {
                var ticker = figiToTicker[c.Key];
                var candles = c.Value.Candles;
                if (candles.Count >= 2)
                {
                    // fill day change
                    {
                        decimal volume = 0;
                        for (int j = 0; j < candles.Count; ++j)
                            volume += candles[j].Volume;

                        if (volume >= 10000 && candles[candles.Count - 1].Volume > 1)
                            day1Change.Add(new Stat(ticker, candles[0].Close, candles[candles.Count - 1].Close));
                    }

                    // fill 1 hour change
                    {
                        var start_time = DateTime.Now.ToUniversalTime().AddHours(-1);
                        var index = candles.FindIndex(x => x.Time >= start_time);
                        if (index >= 0)
                        {
                            decimal volume = 0;
                            for (int j = index; j < candles.Count; ++j)
                                volume += candles[j].Volume;

                            if (volume >= 2000 && candles[candles.Count - 1].Volume > 1)
                                hour1Change.Add(new Stat(ticker, candles[index].Close, candles[candles.Count - 1].Close));
                        }
                    }

                    // fill 15 min change
                    {
                        var start_time = DateTime.Now.ToUniversalTime().AddMinutes(-20);
                        var index = candles.FindIndex(x => x.Time >= start_time);
                        if (index >= 0)
                        {
                            bool hasZeroCandles = false;
                            decimal volume = 0;
                            for (int j = index; j < candles.Count; ++j)
                            {
                                volume += candles[j].Volume;

                                if (candles[index].Volume < 20 || (candles[index].Open == candles[index].Close && candles[index].Low == candles[index].High))
                                    hasZeroCandles = true;
                            }

                            if (volume >= 1000 && !hasZeroCandles)
                                min15Change.Add(new Stat(ticker, candles[index].Close, candles[candles.Count - 1].Close));
                        }
                    }

                    // fill 5 min change
                    {
                        var start_time = DateTime.Now.ToUniversalTime().AddMinutes(-10);
                        var index = candles.FindIndex(x => x.Time >= start_time);
                        if (index >= 0)
                        {
                            bool hasZeroCandles = false;
                            decimal volume = 0;
                            for (int j = index; j < candles.Count; ++j)
                            {
                                volume += candles[j].Volume;

                                if (candles[index].Volume < 20 || (candles[index].Open == candles[index].Close && candles[index].Low == candles[index].High))
                                    hasZeroCandles = true;
                            }

                            if (volume >= 500 && !hasZeroCandles)
                                min5Change.Add(new Stat(ticker, candles[index].Close, candles[candles.Count - 1].Close));
                        }
                    }

                    //// show leaders for 30 minutes in real time
                    //{
                    //    var start_time = DateTime.Now.ToUniversalTime().AddMinutes(-30);
                    //    var index = candles.FindIndex(x => x.Time >= start_time);
                    //    if (index >= 0)
                    //    {
                    //        decimal volume = 0;
                    //        for (int j = index; j < candles.Count; ++j)
                    //            volume += candles[j].Volume;

                    //        if (volume >= 30)
                    //        {
                    //            decimal delta_grow = candles[candles.Count - 1].Close / candles[index].Low;
                    //            decimal delta_fall = candles[candles.Count - 1].Close / candles[index].High;
                    //            if (delta_grow >= (decimal)1.02)
                    //            {
                    //                var color_before = Console.ForegroundColor;
                    //                Console.ForegroundColor = ConsoleColor.Green;
                    //                Console.WriteLine("{0}: +{1}% ({2} ->> {3})", ticker, Math.Round((delta_grow - 1) * 100, 2), candles[index].Low, candles[candles.Count - 1].Close);
                    //                Console.ForegroundColor = color_before;
                    //            }
                    //            else if (delta_fall <= (decimal)0.98)
                    //            {
                    //                var color_before = Console.ForegroundColor;
                    //                Console.ForegroundColor = ConsoleColor.Red;
                    //                Console.WriteLine("{0}: -{1}% ({2} ->> {3})", ticker, Math.Round((1 - delta_fall) * 100, 2), candles[index].High, candleList.Candles[candles.Count - 1].Close);
                    //                Console.ForegroundColor = color_before;
                    //            }
                    //        }
                    //    }
                    //}
                }
            }

            Console.WriteLine("LastUpdate: {0}               Last quote: {1}", DateTime.Now.ToString("HH:mm:ss"), lastCandleReceived.ToString("HH:mm:ss"));
            ShowStats("Top of change DAY:", day1Change);
            ShowStats("Top of change 1H:", hour1Change);
            ShowStats("Top of change 15M:", min15Change);
            ShowStats("Top of change 5M:", min5Change);
        }

        public static void ShowStats(string message, List<Stat> stat)
        {
            const int cMaxOutput = 12;

            Console.WriteLine(message);
            stat.Sort();

            var j = stat.Count - 1;
            for (var i = 0; i < Math.Min(stat.Count, cMaxOutput); ++i)
            {
                String lOutput = String.Format("{0}:", stat[i].ticker).PadLeft(7);
                lOutput += String.Format("{0}%", stat[i].change).PadLeft(7);
                lOutput += String.Format("({0} ->> {1})", stat[i].open, stat[i].close).PadLeft(20);
                lOutput = lOutput.PadRight(10);

                Console.ForegroundColor = stat[i].change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write(lOutput);

                if (j >= cMaxOutput)
                {
                    String rOutput = String.Format("{0}:", stat[j].ticker).PadLeft(7);
                    rOutput += String.Format("{0}%", stat[j].change).PadLeft(7);
                    rOutput += String.Format("({0} ->> {1})", stat[j].open, stat[j].close).PadLeft(20);

                    Console.ForegroundColor = stat[j].change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.Write(rOutput);
                    --j;
                }

                Console.Write("\n\r");
            }

            Console.ResetColor();
            Console.WriteLine();
        }
    }
}