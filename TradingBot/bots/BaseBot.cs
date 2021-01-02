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
            await Task.Yield();
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
                if (acc.BrokerAccountType == BrokerAccountType.Tinkoff)
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
                var connection = ConnectionFactory.GetFakeConnection(_settings.Token);
                _context = connection.Context;
               Logger.Write("Fake connection created");
            }
            else
            {
                var connection = ConnectionFactory.GetConnection(_settings.Token);
                _context = connection.Context;
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

                // test block
                //{
                //    var start = candles.Trends[0].StartPos;
                //    var end = candles.Raw.Count;
                //    if (end - start > 1)
                //    {
                //        decimal a, b;
                //        decimal step = 1m / 1000;
                //        Helpers.Approximate(candles.Raw, start, end, step, out a, out b);
                //        var change = Helpers.GetChangeInPercent(candles.Candles[candles.Candles.Count - 1]);
                //    }
                //}

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
                    for (int i = Math.Max(1, candles.Candles.Count - 24); i < candles.Candles.Count; ++i)
                    {
                        var candle = candles.Candles[i];
                        if (candle.Volume > 50)
                        {
                            avg += Math.Abs(Helpers.GetChangeInPercent(candle));
                            ++candlesCount;
                        }
                    }

                    if (candlesCount > 0)
                        candles.AvgCandleChange = avg / candlesCount;

                    candles.Trends[0].StartPos = candles.Raw.Count;

                    // add new one
                    candles.Candles.Add(cr.Payload);
                    //candles.Raw.Clear();
                    candles.Raw.Add(new Quotes.Quote(cr.Payload.Close, cr.Payload.Volume, cr.Payload.Time));
                    candles.RawPosStart.Add(candles.Raw.Count);
                }

                // update trends
                //foreach (var it in _candles)
                //{
                //    //Logger.Write("Build trends for {0}", it.Key);

                //    //bool finished = false;
                //    //while (!finished)
                //    {
                //        var lastTrend = candles.Trends[candles.Trends.Count - 1];

                //        // update trend each 10 quotes
                //        if (lastTrend.EndPos + 10 < candles.Raw.Count)
                //        {
                //            // estimate trend with a new range
                //            decimal M, S;
                //            Helpers.GetMS(candles.Raw, lastTrend.StartPos, candles.Raw.Count, out M, out S);

                //            // compare it to existing trend
                //            if (M <= lastTrend.M && S <= lastTrend.S)
                //            {
                //                // it is continuqtion of current trend
                //                lastTrend.EndPos = candles.Raw.Count;
                //                lastTrend.M = M;
                //                lastTrend.S = S;
                //            }
                //        }
                //    }
                //}

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