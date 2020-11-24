using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace TradingBot
{
    //
    // Summary:
    //     Stocks screener. Show day statistic for instuments.
    public class Screener : BaseBot
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
                    change = -Math.Round((1 - c/ o) * 100, 2);
                }
            }

            public int CompareTo(Stat right)
            {
                if (this.change != right.change)
                    return (this.change < right.change) ? 1 : -1;

                return this.figi.CompareTo(right.figi);
            }
        }

        private Dictionary<string, List<CandlePayload>> _candles = new Dictionary<string, List<CandlePayload>>();
        private Dictionary<string, QuoteLogger> _loggers = new Dictionary<string, QuoteLogger>();
        DateTime _lastCandleReceived;

        public Screener(string token, string configPath) : base(token, configPath)
        {
            Logger.Write("Screener created");
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            _context.StreamingEventReceived += OnStreamingEventReceived;
            _context.WebSocketException += OnWebSocketExceptionReceived;
            _context.StreamingClosed += OnStreamingClosedReceived;

            // create loggers
            foreach (var ticker in _watchList)
            {
                var figi = _tickerToFigi[ticker];
                _loggers.Add(_tickerToFigi[ticker], new QuoteLogger(figi, ticker));
            }

            await GetHistory();
            await SubscribeCandles();
        }

        public async Task GetHistory()
        {
            Logger.Write("Query candle history...");

            int idx = 0;
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                bool ok = false;
                while (!ok)
                {
                    try
                    {
                        // query history candles
                        ++idx;
                        var session_begin = DateTime.Today.AddHours(10).ToUniversalTime();
                        var candleList = await _context.MarketCandlesAsync(figi, session_begin, DateTime.Now, CandleInterval.FiveMinutes);
                        _candles.Add(figi, candleList.Candles);

                        ok = true;
                    }
                    catch (OpenApiException)
                    {
                        Logger.Write("Context: waiting after {0} queries....", idx);
                        ok = false;
                        idx = 0;
                        await Task.Delay(30000); // sleep for a while
                    }
                    catch (Exception e)
                    {
                        Logger.Write("Excetion: " + e.Message);
                    }
                }
            }

            Logger.Write("Done query candle history...");
        }

        public override void ShowStatus()
        {
            Console.Clear();
            List<Stat> day1Change = new List<Stat>();
            List<Stat> hour1Change = new List<Stat>();
            List<Stat> min15Change = new List<Stat>();
            List<Stat> min5Change = new List<Stat>();

            foreach (var c in _candles)
            {
                var figi = c.Key;
                var candles = c.Value;
                if (candles.Count > 2)
                {
                    // fill day change
                    {
                        decimal volume = 0;
                        for (int j = 0; j < candles.Count; ++j)
                            volume += candles[j].Volume;

                        if (volume >= 1000)
                            day1Change.Add(new Stat(figi, candles[0].Close, candles[candles.Count - 1].Close));
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

                            if (volume >= 100)
                                hour1Change.Add(new Stat(figi, candles[index].Close, candles[candles.Count - 1].Close));
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

                                if (candles[index].Volume < 5 || (candles[index].Open == candles[index].Close && candles[index].Low == candles[index].High))
                                    hasZeroCandles = true;
                            }

                            if (volume >= 100 && !hasZeroCandles)
                                min15Change.Add(new Stat(figi, candles[index].Close, candles[candles.Count - 1].Close));
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

                                if (candles[index].Volume < 10 || (candles[index].Open == candles[index].Close && candles[index].Low == candles[index].High))
                                    hasZeroCandles = true;
                            }

                            if (volume >= 30 && !hasZeroCandles)
                                min5Change.Add(new Stat(figi, candles[index].Close, candles[candles.Count - 1].Close));
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

            Console.WriteLine("LastUpdate: {0}               Last quote: {1}", DateTime.Now.ToString("HH:mm:ss"), _lastCandleReceived.ToString("HH:mm:ss"));
            ShowStats("Top of change DAY:", day1Change);
            ShowStats("Top of change 1H:", hour1Change);
            ShowStats("Top of change 15M:", min15Change);
            ShowStats("Top of change 5M:", min5Change);
        }

        private void ShowStats(string message, List<Stat> stat)
        {
            const int cMaxOutput = 10;
            
            Console.WriteLine(message);            
            stat.Sort();

            var j = stat.Count - 1;
            for (var i = 0; i < Math.Min(stat.Count, cMaxOutput); ++i)
            {
                String lOutput = String.Format("{0}:", _figiToTicker[stat[i].figi]).PadLeft(7);
                lOutput += String.Format("{0}%", stat[i].change).PadLeft(7);
                lOutput += String.Format("({0} ->> {1})", stat[i].open, stat[i].close).PadLeft(20);
                lOutput = lOutput.PadRight(10);

                Console.ForegroundColor = stat[i].change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write(lOutput);

                if (j >= cMaxOutput)
                {
                    String rOutput = String.Format("{0}:", _figiToTicker[stat[j].figi]).PadLeft(7);
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

        private void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                _lastCandleReceived = DateTime.Now;

                var cr = (CandleResponse)e.Response;

                List<CandlePayload> candles;
                if (_candles.TryGetValue(cr.Payload.Figi, out candles))
                {
                    if (candles.Count > 0 && candles[candles.Count - 1].Time == cr.Payload.Time)
                    {
                        // update
                        candles[candles.Count - 1] = cr.Payload;
                    }
                    else
                    {
                        // add new one
                        candles.Add(cr.Payload);
                    }
                }
                else
                {
                    var list = new List<CandlePayload>();
                    list.Add(cr.Payload);
                    _candles.Add(cr.Payload.Figi, list);
                }

                _loggers[cr.Payload.Figi].onQuoteReceived(cr.Payload);
            }
            else
            {
                Logger.Write("Unknown event received: {0}", e.Response);
            }
        }

        private void OnWebSocketExceptionReceived(object s, WebSocketException e)
        {
            Logger.Write("OnWebSocketExceptionReceived: {0}", e.Message);

            Connect();
            _context.StreamingEventReceived += OnStreamingEventReceived;
            _context.WebSocketException += OnWebSocketExceptionReceived;
            _context.StreamingClosed += OnStreamingClosedReceived;

            _ = SubscribeCandles();
        }

        private void OnStreamingClosedReceived(object s, EventArgs args)
        {
            Logger.Write("OnStreamingClosedReceived");
            throw new Exception("Stream closed for unknown reasons...");
        }
    }
}