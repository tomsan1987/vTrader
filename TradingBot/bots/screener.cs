using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace TradingBot
{
    public class Screener : Base_bot
    {
        private class Stat : IComparable<Stat>
        {
            public string figi;
            public decimal change;
            public decimal open;
            public decimal close;

            public Stat(string f, decimal o, decimal c)
            {
                figi = f;
                open = o;
                close = c;

                if (o <= c)
                {
                    change = Math.Round((c / o - 1) * 100, 2);
                }
                else
                {
                    change = Math.Round((1 - c/ o) * 100, 2);
                }
            }

            public int CompareTo(Stat right)
            {
                if (this.change != right.change)
                    return (this.change < right.change) ? 1 : -1;

                return this.figi.CompareTo(right.figi);
            }
        }

        private List<Stat> _day_change = new List<Stat>();
        private List<Stat> _1h_change = new List<Stat>();
        private List<Stat> _5m_change = new List<Stat>();

        public Screener(Context context, string config_path) : base(context, config_path)
        {
        }

        public async Task StartAsync()
        {
            await Query();
            ShowStats();
        }

        public async Task Query()
        {
            int idx = 0;
            for (int i = 0; i < _watch_list.Count; ++i)
            {
                var ticker = _watch_list[i];
                var figi = _ticker_to_figi[ticker];

                bool ok = false;
                while (!ok)
                {
                    try
                    {
                        ++idx;
                        var session_begin = DateTime.Today.AddHours(-10);
                        var candleList = await _context.MarketCandlesAsync(figi, session_begin, DateTime.Now, CandleInterval.FiveMinutes);
                        var candles = candleList.Candles;
                        if (candles.Count > 2)
                        {
                            // fill day change
                            _day_change.Add(new Stat(candleList.Figi, candles[0].Close, candles[candles.Count - 1].Close));

                            // fill 1 hour change
                            {
                                var start_time = DateTime.Now;
                                start_time.AddHours(-1);
                                var index = candles.FindIndex(x => x.Time >= start_time);
                                if (index >=0)
                                {
                                    _1h_change.Add(new Stat(candleList.Figi, candles[index].Close, candles[candles.Count - 1].Close));
                                }
                            }

                            // fill 5 min change
                            {
                                var start_time = DateTime.Now;
                                start_time.AddMinutes(-5);
                                var index = candles.FindIndex(x => x.Time >= start_time);
                                if (index >= 0)
                                {
                                    _5m_change.Add(new Stat(candleList.Figi, candles[index].Close, candles[candles.Count - 1].Close));
                                }
                            }

                            // show leaders for 30 minutes in real time
                            {
                                var start_time = DateTime.Now;
                                start_time.AddMinutes(-30);
                                var index = candles.FindIndex(x => x.Time >= start_time);
                                if (index >= 0)
                                {
                                    decimal delta_grow = candles[candles.Count - 1].Close / candles[index].Low;
                                    decimal delta_fall = candles[candles.Count - 1].Close / candles[index].High;
                                    if (delta_grow >= (decimal)1.02)
                                    {
                                        var color_before = Console.ForegroundColor;
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine("{0}: +{1}% ({2} ->> {3})", ticker, Math.Round((delta_grow - 1) * 100, 2), candles[index].Low, candles[candles.Count - 1].Close);
                                        Console.ForegroundColor = color_before;
                                    }
                                    else if (delta_fall <= (decimal)0.98)
                                    {
                                        var color_before = Console.ForegroundColor;
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine("{0}: -{1}% ({2} ->> {3})", ticker, Math.Round((1 - delta_fall) * 100, 2), candles[index].High, candleList.Candles[candles.Count - 1].Close);
                                        Console.ForegroundColor = color_before;
                                    }
                                }
                            }
                        }

                        ok = true;
                    }
                    catch (OpenApiException ex)
                    {
                        Console.WriteLine("Context: waiting after {0} queries....", idx);
                        ok = false;
                        idx = 0;
                        await Task.Delay(30000); // sleep for a while
                    }
                }
            }

            Console.WriteLine("Finished");
        }

        public async void ShowStats()
        {
            _day_change.Sort();
            _1h_change.Sort();
            _5m_change.Sort();

            ShowStats("Top {} of grow DAY:", _day_change);
            ShowStats("Top {} of grow 1H:", _1h_change);
            ShowStats("Top {} of grow 5M:", _5m_change);
        }

        private void ShowStats(string message, List<Stat> stat)
        {
            int COUNT = Math.Min(stat.Count, 10);

            var color_before = Console.ForegroundColor;

            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.Green;
            for (var i = 0; i < COUNT; ++i)
            {
                Console.ForegroundColor = stat[i].change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine("{0}: +{1}% ({2} ->> {3})", _figi_to_ticker[stat[i].figi], stat[i].change, stat[i].open, stat[i].close);
            }


            for (var i = stat.Count - COUNT; i < stat.Count; ++i)
            {
                Console.ForegroundColor = stat[i].change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine("{0}: -{1}% ({2} ->> {3})", _figi_to_ticker[stat[i].figi], stat[i].change, stat[i].open, stat[i].close);
            }

            Console.ForegroundColor = color_before;
        }
    }
}