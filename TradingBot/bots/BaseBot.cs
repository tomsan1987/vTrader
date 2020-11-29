using System;
using System.IO;
using System.Net.WebSockets;
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
    //     Base class for all trade bots.
    public class BaseBot : IAsyncDisposable
    {
        protected IContext _context;
        protected string _token;
        protected string _accountId;
        protected List<MarketInstrument> _instruments;
        protected IList<string> _watchList;
        protected Dictionary<string, string> _figiToTicker = new Dictionary<string, string>();
        protected Dictionary<string, MarketInstrument> _figiToInstrument = new Dictionary<string, MarketInstrument>();
        protected Dictionary<string, string> _tickerToFigi = new Dictionary<string, string>();
        protected Dictionary<string, Quotes> _candles;
        protected DateTime _lastCandleReceived;
        protected string _configPath;

        public BaseBot(string token, string configPath)
        {
            _token = token;
            _configPath = configPath;
        }

        public virtual async Task StartAsync()
        {
            Connect();
            await Init();
            InitCandles();

            //_context.StreamingEventReceived += OnStreamingEventReceived;
            //_context.WebSocketException += OnWebSocketExceptionReceived;
            //_context.StreamingClosed += OnStreamingClosedReceived;
        }

        public virtual void ShowStatus()
        {        
        }

        public virtual async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }

        protected async Task Init()
        {
            var configJson = JObject.Parse(File.ReadAllText(_configPath));
            _watchList = ((JArray)configJson["watch-list"]).ToObject<IList<string>>();

            // get account ID
            var accounts = await _context.AccountsAsync();
            foreach (var acc in accounts)
            {
                _accountId = acc.BrokerAccountId;
            }

            var stocks = await _context.MarketStocksAsync();
            _instruments = stocks.Instruments;

            foreach (var ticker in _watchList)
            {
                var idx = _instruments.FindIndex(x => x.Ticker == ticker);
                if (idx != -1)
                {
                    _figiToTicker.Add(_instruments[idx].Figi, _instruments[idx].Ticker);
                    _tickerToFigi.Add(_instruments[idx].Ticker, _instruments[idx].Figi);
                    _figiToInstrument.Add(_instruments[idx].Figi, _instruments[idx]);
                }
                else
                {
                    throw new Exception("Unknown ticker: " + ticker);
                }
            }
        }

        protected void Connect()
        {
            //var connection = ConnectionFactory.GetConnection(_token);
            var connection = ConnectionFactory.GetFakeConnection(_token);
            _context = connection.Context;
            Logger.Write("Connection created");
        }

        protected virtual void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                _lastCandleReceived = DateTime.Now;

                var cr = (CandleResponse)e.Response;

                var candles = _candles[cr.Payload.Figi];
                if (candles.Candles.Count > 0 && candles.Candles[candles.Candles.Count - 1].Time == cr.Payload.Time)
                {
                    // update
                    candles.Candles[candles.Candles.Count - 1] = cr.Payload;
                    candles.Raw.Add(new Quotes.Quote(cr.Payload.Close, cr.Payload.Volume));
                }
                else
                {
                    // add new one
                    candles.Candles.Add(cr.Payload);
                    candles.Raw.Clear();
                    candles.Raw.Add(new Quotes.Quote(cr.Payload.Close, cr.Payload.Volume));
                }

                candles.QuoteLogger.onQuoteReceived(cr.Payload);
            }
            else
            {
                Logger.Write("Unknown event received: {0}", e.Response);
            }
        }

        protected void OnWebSocketExceptionReceived(object s, WebSocketException e)
        {
            Logger.Write("OnWebSocketExceptionReceived: {0}", e.Message);

            Connect();
            _context.StreamingEventReceived += OnStreamingEventReceived;
            _context.WebSocketException += OnWebSocketExceptionReceived;
            _context.StreamingClosed += OnStreamingClosedReceived;

            _ = SubscribeCandles();
        }

        protected void OnStreamingClosedReceived(object s, EventArgs args)
        {
            Logger.Write("OnStreamingClosedReceived");
            throw new Exception("Stream closed for unknown reasons...");
        }

        protected async Task SubscribeCandles()
        {
            Logger.Write("Start subscribing candles...");

            for (int i = 0; i < _watchList.Count; ++i)
                await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(_tickerToFigi[_watchList[i]], CandleInterval.FiveMinutes));

            Logger.Write("End of subscribing candles...");
        }

        protected void InitCandles()
        {
            _candles = new Dictionary<string, Quotes>();
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];
                _candles.Add(figi, new Quotes(figi, ticker));
            }
        }
    }
}