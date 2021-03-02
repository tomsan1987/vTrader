using System;
using System.IO;
using System.Net.WebSockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json.Linq;

namespace TradingBot
{
    //
    // Summary:
    //     Base class for all trade bots.
    public class BaseBot : IAsyncDisposable
    {
        protected FakeConnection _fakeConnection;
        protected Connection _connection;
        protected IContext _context;
        protected Settings _settings;
        protected string _accountId;
        protected List<MarketInstrument> _instruments;
        protected IList<string> _watchList;
        protected Dictionary<string, string> _figiToTicker = new Dictionary<string, string>();
        protected Dictionary<string, MarketInstrument> _figiToInstrument = new Dictionary<string, MarketInstrument>();
        protected Dictionary<string, string> _tickerToFigi = new Dictionary<string, string>();
        protected Dictionary<string, Quotes> _candles;
        protected DateTime _lastCandleReceived;

        public class Settings
        {
            public bool SubscribeQuotes { get; set; } = false;
            public bool RequestCandlesHistory { get; set; } = false;
            public bool FakeConnection { get; set; } = true;
            public bool DumpQuotes { get; set; } = true;
            public string Token { get; set; }
            public string ConfigPath { get; set; }
            public string Strategies { get; set; }

            public Settings(ProgramOptions po)
            {
                if (po.HasValue("SubscribeQuotes"))
                    SubscribeQuotes = po.Get<bool>("SubscribeQuotes");

                if (po.HasValue("RequestCandlesHistory"))
                    RequestCandlesHistory = po.Get<bool>("RequestCandlesHistory");

                if (po.HasValue("FakeConnection"))
                    FakeConnection = po.Get<bool>("FakeConnection");

                if (po.HasValue("DumpQuotes"))
                    DumpQuotes = po.Get<bool>("DumpQuotes");

                if (po.HasValue("Token"))
                    Token = (File.ReadAllText(po.Get<string>("Token"))).Trim();

                if (po.HasValue("ConfigPath"))
                    ConfigPath = po.Get<string>("ConfigPath");

                if (po.HasValue("Strategies"))
                    Strategies = po.Get<string>("Strategies");
            }
        }

        public BaseBot(Settings settings)
        {
            _settings = settings;
        }

        public virtual async Task StartAsync()
        {
            Connect();
            await InitInstruments();
            InitCandles();
            await RequestCandleHistory();
            await SubscribeCandles();
        }

        public virtual void ShowStatus()
        {
        }

        public virtual async ValueTask DisposeAsync()
        {
            await UnSubscribeCandles();
            _fakeConnection?.Dispose();
            _connection?.Dispose();

            foreach (var it in _candles)
                it.Value.Dispose();
        }

        protected async Task InitInstruments()
        {
            if (!File.Exists(_settings.ConfigPath))
                throw new Exception("Configuration file " + _settings.ConfigPath + " does not exists!");

            var configJson = JObject.Parse(File.ReadAllText(_settings.ConfigPath));
            _watchList = ((JArray)configJson["watch-list"]).ToObject<IList<string>>();

            // get account ID
            var accounts = await _context.AccountsAsync();
            foreach (var acc in accounts)
            {
                if (acc.BrokerAccountType == BrokerAccountType.TinkoffIis)
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
            if (_settings.FakeConnection)
            {
                _fakeConnection?.Dispose();
                _fakeConnection = ConnectionFactory.GetFakeConnection(_settings.Token);
                _context = _fakeConnection.Context;
               Logger.Write("Fake connection created");
            }
            else
            {
                _connection?.Dispose();
                _connection = ConnectionFactory.GetConnection(_settings.Token);
                _context = _connection.Context;
                Logger.Write("Real connection created");
            }
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
                    var volume = cr.Payload.Volume - candles.Candles[candles.Candles.Count - 1].Volume;
                    candles.Candles[candles.Candles.Count - 1] = cr.Payload;
                    candles.Raw.Add(new Quotes.Quote(cr.Payload.Close, volume, cr.Payload.Time));
                }
                else
                {
                    decimal avg = 0;
                    int candlesCount = 0;
                    for (int i = Math.Max(1, candles.Candles.Count - 12); i < candles.Candles.Count; ++i)
                    {
                        var candle = candles.Candles[i];
                        if (candle.Volume > 50)
                        {
                            avg += Math.Abs(Helpers.GetChangeInPercent(candle));
                            ++candlesCount;
                        }
                    }

                    if (candlesCount > 0)
                        candles.AvgCandleChange = Math.Round(avg / candlesCount, 2);

                    // add new one
                    candles.Candles.Add(cr.Payload);
                    candles.Raw.Add(new Quotes.Quote(cr.Payload.Close, cr.Payload.Volume, cr.Payload.Time));
                    candles.RawPosStart.Add(candles.Raw.Count);
                }

                candles.DayMax = Math.Max(candles.DayMax, cr.Payload.Close);
                candles.DayMin = Math.Min(candles.DayMin, cr.Payload.Close);

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
           _ =  UnSubscribeCandles();

            Connect();
            _ = SubscribeCandles();
        }

        protected void OnStreamingClosedReceived(object s, EventArgs args)
        {
            Logger.Write("OnStreamingClosedReceived");
            throw new Exception("Stream closed for unknown reasons...");
        }

        private async Task SubscribeCandles()
        {
            if (_settings.SubscribeQuotes)
            {
                _context.StreamingEventReceived += OnStreamingEventReceived;
                _context.WebSocketException += OnWebSocketExceptionReceived;
                _context.StreamingClosed += OnStreamingClosedReceived;

                Logger.Write("Start subscribing candles...");

                for (int i = 0; i < _watchList.Count; ++i)
                    await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(_tickerToFigi[_watchList[i]], CandleInterval.FiveMinutes));

                Logger.Write("End of subscribing candles...");
            }
        }

        private async Task UnSubscribeCandles()
        {
            if (_settings.SubscribeQuotes && _context != null)
            {
                _context.StreamingEventReceived -= OnStreamingEventReceived;
                _context.WebSocketException -= OnWebSocketExceptionReceived;
                _context.StreamingClosed -= OnStreamingClosedReceived;

                Logger.Write("Start unsubscribing candles...");

                for (int i = 0; i < _watchList.Count; ++i)
                    await _context.SendStreamingRequestAsync(StreamingRequest.UnsubscribeCandle(_tickerToFigi[_watchList[i]], CandleInterval.FiveMinutes));

                Logger.Write("End of unsubscribing candles...");
            }
        }

        private async Task RequestCandleHistory()
        {
            if (_settings.RequestCandlesHistory)
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
                            var sessionBegin = DateTime.Today.AddHours(10).ToUniversalTime();
                            if (DateTime.Now.Hour < 3) // in case app run after midnight
                                sessionBegin = sessionBegin.AddDays(-1);
                            var candleList = await _context.MarketCandlesAsync(figi, sessionBegin, DateTime.Now, CandleInterval.FiveMinutes);
                            _candles[figi].Candles = candleList.Candles;

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
        }

        protected void InitCandles()
        {
            if (_candles != null)
            {
                foreach (var it in _candles)
                    it.Value.Dispose();
            }

            _candles = new Dictionary<string, Quotes>();
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];
                _candles.Add(figi, new Quotes(figi, ticker, _settings.DumpQuotes));
            }
        }
    }
}