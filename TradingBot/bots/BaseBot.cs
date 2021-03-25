using System;
using System.IO;
using System.Net.WebSockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Timers;

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
        protected Timer _subscriptionTimer = new System.Timers.Timer();
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
            SubscribeCandles();
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

            Logger.Write("BaseBot::DisposeAsync done!");
        }

        protected class OperationStat : IComparable<OperationStat>
        {
            public string ticker = "";
            public Currency currency;
            public decimal profit = 0;
            public decimal commission = 0;
            public decimal dividents = 0;
            public int operations = 0;
            public int CompareTo(OperationStat right)
            {
                if (this.currency != right.currency)
                    return (this.currency < right.currency) ? 1 : -1;

                var sumL = this.profit + this.commission + this.dividents;
                var sumR = right.profit + right.commission + right.dividents;
                if (sumL != sumR)
                    return (sumL < sumR) ? 1 : -1;

                return this.ticker.CompareTo(right.ticker);
            }
        }

        public async Task OperationReport()
        {
            DateTime from = new DateTime(2020, 1, 1, 0, 0, 0).ToUniversalTime();
            DateTime to = DateTime.UtcNow;

            //string figi = _context.MarketSearchByTickerAsync("HYDR").Result.Instruments[0].Ticker;
            string figi = "";

            List<Operation> operations = new List<Operation>();
            var accounts = await _context.AccountsAsync();
            foreach (var acc in accounts)
            {
                var res = await _context.OperationsAsync(from, DateTime.UtcNow, figi, acc.BrokerAccountId);
                operations.InsertRange(operations.Count, res);
            }

            Dictionary<string, OperationStat> operationsReport = new Dictionary<string, OperationStat>();
            foreach (var it in operations)
            {
                if (it.Figi == null)
                    continue;

                if (!operationsReport.ContainsKey(it.Figi))
                {
                    operationsReport.Add(it.Figi, new OperationStat());
                }

                if (it.Status == OperationStatus.Done)
                {
                    if (it.OperationType == ExtendedOperationType.Buy || it.OperationType == ExtendedOperationType.BuyCard || it.OperationType == ExtendedOperationType.Sell)
                    {
                        operationsReport[it.Figi].profit += it.Payment;
                        operationsReport[it.Figi].operations++;
                    }
                    else if (it.OperationType == ExtendedOperationType.BrokerCommission)
                    {
                        operationsReport[it.Figi].commission += it.Payment;
                    }
                    else if (it.OperationType == ExtendedOperationType.Dividend)
                    {
                        operationsReport[it.Figi].dividents += it.Payment;
                    }
                }
            }

            // convert to list and sort
            List<OperationStat> result = new List<OperationStat>();
            foreach (var it in operationsReport)
            {
                if (it.Value.operations > 0)
                {
                    var instrument = _context.MarketSearchByFigiAsync(it.Key).Result;
                    it.Value.ticker = instrument.Ticker;
                    it.Value.currency = instrument.Currency;

                    result.Add(it.Value);
                }
            }

            // write header
            var writer = new StreamWriter("operation_report_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv", false);
            writer.AutoFlush = true;
            writer.WriteLine("Ticker;TotalOrders;Profit;Dividends;Commission;Total;Currency");

            result.Sort();
            foreach (var it in result)
                writer.WriteLine("{0};{1};{2};{3};{4};{5};{6}", it.ticker, it.operations, it.profit, it.dividents, it.commission, it.profit + it.dividents + it.commission, Utils.ToEnumString(it.currency));

            writer.Close();
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
                AddNewCandle(cr.Payload);

                _candles[cr.Payload.Figi].QuoteLogger.onQuoteReceived(cr.Payload);
            }
            else if (e.Response.Event == "error")
            {
                var res = (StreamingErrorResponse)e.Response;
                Logger.Write("Error event received: {0}", res.Payload.Error);
            }
            else
            {
                Logger.Write("Unknown event received: {0}", e.Response);
            }
        }

        protected void OnWebSocketExceptionReceived(object s, WebSocketException e)
        {
            Logger.Write("OnWebSocketExceptionReceived: {0}", e.Message);
            //_ =  UnSubscribeCandles();

            // Connect();
            // _ = SubscribeCandles();
            _subscriptionTimer.Stop();
            _subscriptionTimer.Start();
        }

        protected void OnStreamingClosedReceived(object s, EventArgs args)
        {
            Logger.Write("OnStreamingClosedReceived");
            throw new Exception("Stream closed for unknown reasons...");
        }

        private void SubscribeCandles()
        {
            if (_settings.SubscribeQuotes)
            {
                _context.StreamingEventReceived += OnStreamingEventReceived;
                _context.WebSocketException += OnWebSocketExceptionReceived;
                _context.StreamingClosed += OnStreamingClosedReceived;

                _subscriptionTimer.AutoReset = false;
                _subscriptionTimer.Elapsed += new ElapsedEventHandler(SubscribeCandlesImpl);
                _subscriptionTimer.Interval = 5000;
                _subscriptionTimer.Start();

                //_candleSubscriptionTask = Task.Run(async () =>
                //{
                //    while (!_disposing)
                //    {
                //        Dictionary<string, int> newQuotes = null;
                //        lock (_quotesLock)
                //        {
                //            newQuotes = _newQuotes;
                //            _newQuotes = new Dictionary<string, int>();
                //        }

                //        foreach (var it in newQuotes)
                //        {
                //            if (it.Value > 5)
                //                Logger.Write("{0}: Quotes queue size: {1}", _figiToTicker[it.Key], it.Value);

                //            await OnCandleUpdate(it.Key);
                //        }
                //    }
                //});
            }
        }

        private void SubscribeCandlesImpl(object source, ElapsedEventArgs e)
        {
            try
            {
                Logger.Write("Start subscribing candles...");

                for (int i = 0; i < _watchList.Count; ++i)
                    _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(_tickerToFigi[_watchList[i]], CandleInterval.FiveMinutes)).Wait();

                Logger.Write("End of subscribing candles...");
            }
            catch (Exception ex)
            {
                Logger.Write("Error while subscribing candles: " + ex.Message);
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
                            if (DateTime.Now.Hour < 3) // in case App run after midnight
                                sessionBegin = sessionBegin.AddDays(-1);
                            var candleList = await _context.MarketCandlesAsync(figi, sessionBegin, DateTime.Now, CandleInterval.FiveMinutes);

                            foreach (var it in candleList.Candles)
                                AddNewCandle(it);

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
                            Logger.Write("Exception: " + e.Message);
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

        private void AddNewCandle(CandlePayload candle)
        {
            var candles = _candles[candle.Figi];

            if (candles.Candles.Count > 0 && candles.Candles[candles.Candles.Count - 1].Time == candle.Time)
            {
                // update
                var volume = candle.Volume - candles.Candles[candles.Candles.Count - 1].Volume;
                candles.Candles[candles.Candles.Count - 1] = candle;
                candles.Raw.Add(new Quotes.Quote(candle.Close, volume, candle.Time));
            }
            else
            {
                decimal avg = 0;
                int candlesCount = 0;
                for (int i = Math.Max(1, candles.Candles.Count - 12); i < candles.Candles.Count; ++i)
                {
                    var c = candles.Candles[i];
                    if (c.Volume > 50)
                    {
                        avg += Math.Abs(Helpers.GetChangeInPercent(c));
                        ++candlesCount;
                    }
                }

                if (candlesCount > 0)
                    candles.AvgCandleChange = Math.Round(avg / candlesCount, 2);

                // add new one
                candles.Candles.Add(candle);
                candles.Raw.Add(new Quotes.Quote(candle.Close, candle.Volume, candle.Time));
                candles.RawPosStart.Add(candles.Raw.Count);
            }

            candles.DayMax = Math.Max(candles.DayMax, candle.Close);
            candles.DayMin = Math.Min(candles.DayMin, candle.Close);
        }
    }
}