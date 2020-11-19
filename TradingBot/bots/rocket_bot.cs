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
        private Dictionary<string, CandlePayload> _quotes = new Dictionary<string, CandlePayload>();
        private Dictionary<string, Quote_logger> _loggers = new Dictionary<string, Quote_logger>();
        //PlacedLimitOrder _order;


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
                            _loggers.Add(figi, new Quote_logger(figi, ticker));
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

                    //var res = _context.CancelOrderAsync(_order.OrderId);
                    //res.Wait();
                    //var test = 0;
                }
                else
                {
                    _quotes.Add(cr.Payload.Figi, cr.Payload);

                    try
                    {
                        // place limit order BUY
                        //var price = Helpers.round_price(cr.Payload.Close * (decimal)0.9, get_min_increment(cr.Payload.Figi));
                        //LimitOrder order = new LimitOrder(cr.Payload.Figi, 1, OperationType.Buy, price);
                        //var res = _context.PlaceLimitOrderAsync(order);
                        //res.Wait();
                        //_order = res.Result;

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                Console.WriteLine("{0}:{1}", _figi_to_ticker[cr.Payload.Figi], cr.Payload.Close);

                _loggers[cr.Payload.Figi].on_quote_received(cr);
            }
            else
            {
                Console.WriteLine(e.Response);
                //_work = false;
            }
        }
    }
}