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
    //
    // Summary:
    //     The Bot subcribes for instruments from watch list and keeps track of abrupt change of price.
    public class RocketBot : BaseBot
    {
        private Dictionary<string, CandlePayload> _quotes = new Dictionary<string, CandlePayload>();
        private Dictionary<string, QuoteLogger> _loggers = new Dictionary<string, QuoteLogger>();
        //PlacedLimitOrder _order;


        public RocketBot(Context context, string config_path) : base(context, config_path)
        {
        }

        public async Task StartAsync()
        {
            await SubscribeCandles();
        }

        public async Task SubscribeCandles()
        {
            Console.WriteLine("Start subscribing candles...");

            // subscribe to candles
            _context.StreamingEventReceived += OnStreamingEventReceived;

            int processed = 0;
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];
                if (figi.Length > 0)
                {
                    bool ok = false;
                    while (!ok)
                    {
                        try
                        {
                            await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(figi, CandleInterval.Minute));
                            ok = true;
                            _loggers.Add(figi, new QuoteLogger(figi, ticker));
                        }
                        catch (OpenApiException)
                        {
                            Console.WriteLine("Context: waiting after {0} queries....", processed);
                            ok = false;
                            await Task.Delay(30000); // sleep for a while
                        }
                    }

                    ++processed;
                }
            }

            Console.WriteLine("End of subscribing candles...");
        }

        private void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                var cr = (CandleResponse)e.Response;

                CandlePayload prev;
                if (_quotes.TryGetValue(cr.Payload.Figi, out prev))
                {
                    var q = _quotes[cr.Payload.Figi];
                    if (q.Time.Minute == cr.Payload.Time.Minute)
                    {
                        // update current candle
                        q = cr.Payload;
                    }
                    else
                    {
                    }
                }
                else
                {

                }

                Console.WriteLine("{0}:{1}", _figiToTicker[cr.Payload.Figi], cr.Payload.Close);

                _loggers[cr.Payload.Figi].onQuoteReceived(cr);
            }
            else
            {
                Console.WriteLine(e.Response);
            }
        }
    }
}