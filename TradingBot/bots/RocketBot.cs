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
    //     The Bot subcribes for instruments from watch list and keeps track of abrupt change of price.
    public class RocketBot : BaseBot
    {
        private Dictionary<string, List<CandlePayload>> _candles = new Dictionary<string, List<CandlePayload>>();
        private Dictionary<string, QuoteLogger> _loggers = new Dictionary<string, QuoteLogger>();
        DateTime _lastCandleReceived;

        public RocketBot(string token, string config_path) : base(token, config_path)
        {
            Logger.Write("Rocket bot created");
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

            await SubscribeCandles();
        }

        public override void ShowStatus()
        {
            Console.Clear();
            Console.WriteLine("Time of last quote received: {0}", _lastCandleReceived.ToString("HH:mm:ss"));
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

                _loggers[cr.Payload.Figi].onQuoteReceived(cr);
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