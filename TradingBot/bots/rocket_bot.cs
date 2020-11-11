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
    public class Rocket_bot : Base_bot
    {
        private bool _work = true;
        private Dictionary<string, CandlePayload> _quotes = new Dictionary<string, CandlePayload>();

        public Rocket_bot(Context context, string config_path) : base(context, config_path)
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
            for (int i = 0; i < _watch_list.Count; ++i)
            {
                var ticker = _watch_list[i];
                var figi = _ticker_to_figi[ticker];
                if (figi.Length > 0)
                {
                    bool ok = false;
                    while (!ok)
                    {
                        try
                        {
                            await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(figi, CandleInterval.Minute));
                            ok = true;
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
                        //q.Close = cr.Payload.Close;
                    }
                }
                else
                {
                    _quotes.Add(cr.Payload.Figi, cr.Payload);
                }

                Console.WriteLine("{0}:{1}", _figi_to_ticker[cr.Payload.Figi], cr.Payload.Close);
            }
            else
            {
                Console.WriteLine(e.Response);
                //_work = false;
            }
        }
    }
}