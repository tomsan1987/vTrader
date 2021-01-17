using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json;

namespace TradingBot
{
    //
    // Summary:
    //     The Bot subscribes for instruments from watch list and keeps track of abrupt change of price.
    public class TradeBot : BaseBot
    {
        private Dictionary<string, TradeData> _tradeData = new Dictionary<string, TradeData>();
        private TradeStatistic _stats = new TradeStatistic();
        private bool _isShutingDown = false;
        private Task _tradeTask;
        private Dictionary<string, int> _newQuotes = new Dictionary<string, int>();
        private object _quotesLock = new object();
        private AutoResetEvent _quoteReceivedEvent = new AutoResetEvent(false);
        private AutoResetEvent _quoteProcessedEvent = new AutoResetEvent(false);
        private IStrategy[] _strategies;
        private DateTime _lastTimeOut = DateTime.UtcNow.AddMinutes(-1);

        public TradeBot(Settings settings) : base(settings)
        {
            Logger.Write("Trade bot created");

            if (settings.Strategies != null && settings.Strategies.Length > 0)
            {
                var strategyNames = settings.Strategies.Split(",");
                _strategies = new IStrategy[strategyNames.Length];

                for (int i = 0; i < strategyNames.Length; ++i)
                {
                    switch (strategyNames[i])
                    {
                        case "RocketStrategy": _strategies[i] = new RocketStrategy(); break;
                        case "GoodGrowStrategy": _strategies[i] = new GoodGrowStrategy(); break;
                        case "ImpulseStrategy": _strategies[i] = new ImpulseStrategy(); break;
                        case "SpykeStrategy": _strategies[i] = new SpykeStrategy(); break;
                        default: Logger.Write("Unknown strategy name '{0}'", strategyNames[i]); break;
                    }
                }
            }
            else
            {
                Logger.Write("No strategies specified!");
                throw new Exception("No strategies specified!");
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (_settings.FakeConnection)
            {
                await CloseAll();
                var loggerFileName = Logger.FileName().Substring(0, Logger.FileName().Length - 4);

                // orders statistic
                {
                    String message = "";
                    foreach (var it in _stats.logMessages)
                    {
                        message += it;
                        message += "\n";
                    }

                    var orderStatisticFile = new StreamWriter(loggerFileName + "_OS.csv", false);
                    orderStatisticFile.Write(message);
                    orderStatisticFile.Flush();
                    orderStatisticFile.Close();
                }

                // volume distribution
                var volumeDistributionFile = new StreamWriter(loggerFileName + "_VD.csv", false);
                volumeDistributionFile.Write(_stats.GetVolumeDistribution());
                volumeDistributionFile.Flush();
                volumeDistributionFile.Close();

                // common stat
                Logger.Write(_stats.GetStringStat());
            }

            await base.DisposeAsync();
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            foreach (var ticker in _watchList)
            {
                _tradeData.Add(_tickerToFigi[ticker], new TradeData());
            }

            _tradeTask = Task.Run(async () =>
            {
                while (_quoteReceivedEvent.WaitOne())
                {
                    Dictionary<string, int> newQuotes = null;
                    lock (_quotesLock)
                    {
                        newQuotes = _newQuotes;
                        _newQuotes = new Dictionary<string, int>();
                    }

                    foreach (var it in newQuotes)
                    {
                        if (it.Value > 5)
                            Logger.Write("{0}: Quotes queue size: {1}", _figiToTicker[it.Key], it.Value);

                        await OnCandleUpdate(it.Key);
                    }

                    _quoteProcessedEvent.Set();
                }
            });
        }

        public override void ShowStatus()
        {
            Console.Clear();
            Screener.ShowStatusStatic(_figiToTicker, _lastCandleReceived, _candles);
        }

        private async Task OnCandleUpdate(string figi)
        {
            try
            {
                if (_lastTimeOut.AddSeconds(15) > DateTime.UtcNow)
                    return;

                // at the moment we trade only in USD
                if (_figiToInstrument[figi].Currency != Currency.Usd)
                    return;

                if (_isShutingDown)
                    return;

                var tradeData = _tradeData[figi];
                var instrument = _figiToInstrument[figi];
                var candles = _candles[figi].Candles;
                var candle = candles[candles.Count - 1];

                if (_candles[figi].IsSpike())
                {
                    Logger.Write("{0}: SPYKE!. Candle: {1}", instrument.Ticker, JsonConvert.SerializeObject(candle));
                    return;
                }

                if (_settings.SubscribeQuotes)
                {
                    // make sure that we got actual candle
                    TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.AddMinutes(-5).ToUniversalTime().Ticks - candle.Time.Ticks);
                    if (elapsedSpan.TotalSeconds > 5)
                        return;
                }

                if (tradeData.Status == Status.Watching)
                {
                    foreach (var strategy in _strategies)
                    {
                        if (strategy != null && strategy.Process(instrument, tradeData, _candles[figi]) == IStrategy.StrategyResultType.Buy)
                        {
                            var order = new LimitOrder(instrument.Figi, 1, OperationType.Buy, tradeData.BuyPrice);
                            var placedOrder = await _context.PlaceLimitOrderAsync(order);

                            tradeData.Strategy = strategy;
                            tradeData.OrderId = placedOrder.OrderId;
                            tradeData.Status = Status.BuyPending;
                            tradeData.Time = candle.Time;
                            tradeData.BuyTime = candle.Time;
                            Logger.Write("{0}: OrderId: {1}. Details: Status - {2}, RejectReason -  {3}", instrument.Ticker, tradeData.OrderId, placedOrder.Status.ToString(), placedOrder.RejectReason);
                        }
                    }
                }
                else if (tradeData.Status == Status.BuyPending)
                {
                    // check if limited order executed
                    if (await IsOrderExecuted(figi, tradeData.OrderId))
                    {
                        // order executed
                        tradeData.Status = Status.BuyDone;
                        tradeData.Time = candle.Time;
                        _stats.Buy(tradeData.BuyPrice);

                        Logger.Write("{0}: OrderId: {1} executed", instrument.Ticker, tradeData.OrderId);
                    }
                    else
                    {
                        bool cancel = true;
                        if (tradeData.Strategy is SpykeStrategy)
                        {
                            if (tradeData.Strategy.Process(instrument, tradeData, _candles[figi]) != IStrategy.StrategyResultType.CancelOrder)
                            {
                                cancel = false;
                            }
                        }

                        if (cancel)
                        {
                            // check if price changed is not significantly
                            var change = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                            if (change >= 0.5m)
                            {
                                Logger.Write("{0}: Cancel order. Candle: ID:{1}, Time: {2}, Close: {3}. Details: price change {4}", instrument.Ticker, _candles[figi].Raw.Count, candle.Time.ToShortTimeString(), candle.Close, change);

                                await _context.CancelOrderAsync(tradeData.OrderId);
                                tradeData.OrderId = null;
                                tradeData.BuyPrice = 0;
                                tradeData.Status = Status.Watching;
                                tradeData.Time = candle.Time.AddMinutes(-15); // this is for try to buy it again
                            }
                        }
                    }
                }
                else if (tradeData.Status == Status.BuyDone)
                {
                    if (tradeData.Strategy.Process(instrument, tradeData, _candles[figi]) == IStrategy.StrategyResultType.Sell)
                    {
                        // sell 1 lot
                        var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, tradeData.SellPrice);
                        var placedOrder = await _context.PlaceLimitOrderAsync(order);

                        tradeData.Status = Status.SellPending;
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: OrderId: {1}. Details: Status - {2}, RejectReason -  {3}", instrument.Ticker, tradeData.OrderId, placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                }
                else if (tradeData.Status == Status.SellPending)
                {
                    // check if limited order executed
                    if (await IsOrderExecuted(figi, tradeData.OrderId))
                    {
                        // order executed
                        Logger.Write("{0}: OrderId: {1} executed", instrument.Ticker, tradeData.OrderId);

                        _stats.Sell(tradeData.BuyTime, candle.Time, tradeData.BuyPrice, instrument.Ticker);
                        _stats.Update(instrument.Ticker, tradeData.BuyPrice, tradeData.SellPrice);
                        tradeData.Reset();
                    }
                    else
                    {
                        // check if price changed is not significantly
                        var change = Helpers.GetChangeInPercent(tradeData.SellPrice, candle.Close);
                        if (change <= -0.2m)
                        {
                            Logger.Write("{0}: Cancel order. Candle: {1}. Details: price change {2}", instrument.Ticker, JsonConvert.SerializeObject(candle), change);

                            await _context.CancelOrderAsync(tradeData.OrderId);
                            tradeData.Status = Status.BuyDone;
                        }
                    }
                }
                else if (tradeData.Status == Status.ShutDown)
                {
                    if (tradeData.BuyPrice != 0)
                    {
                        _stats.Update(instrument.Ticker, tradeData.BuyPrice, candle.Close);
                    }

                    //TODO
                    //break;
                }
            }
            catch (OpenApiException e)
            {
                Logger.Write(e.Message);

                // seems timeout, disable operations for 15 seconds
                if (e.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    _lastTimeOut = DateTime.UtcNow;

                //OrderNotAvailable - no money
            }
            catch (Exception e)
            {
                Logger.Write(e.Message);
            }
        }

        private async Task CloseAll()
        {
            _isShutingDown = true;
            foreach (var it in _tradeData)
            {
                var tradeData = it.Value;
                if (tradeData != null && tradeData.BuyPrice != 0)
                {
                    var candle = _candles[it.Key].Candles[_candles[it.Key].Candles.Count - 1];
                    switch (tradeData.Status)
                    {
                        case Status.BuyPending:
                            {
                                var instrument = _figiToInstrument[it.Key];
                                if (await IsOrderExecuted(it.Key, tradeData.OrderId))
                                {
                                    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                                    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                                    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                                    Logger.Write("{0}: Closing dayly orders. Close price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}. Profit: {5}({6}%)", instrument.Ticker, price, _candles[it.Key].Raw.Count, candle.Time.ToShortTimeString(), candle.Close, price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));

                                    _stats.Sell(tradeData.BuyTime, candle.Time, tradeData.BuyPrice, instrument.Ticker);
                                    _stats.Update(instrument.Ticker, tradeData.BuyPrice, price);
                                }
                                else
                                {
                                    // just cancel order
                                    Logger.Write("{0}: Cancel order. Candle: ID:{1}, Time: {2}, Close: {3}. Details: end of day", instrument.Ticker, _candles[it.Key].Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                                    await _context.CancelOrderAsync(tradeData.OrderId);
                                }
                            }
                            break;

                        case Status.BuyDone:
                            {
                                var instrument = _figiToInstrument[it.Key];
                                var price = candle.Close - 2 * instrument.MinPriceIncrement;
                                var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                                var placedOrder = await _context.PlaceLimitOrderAsync(order);

                                Logger.Write("{0}: Closing dayly orders. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));

                                _stats.Sell(tradeData.BuyTime, candle.Time, tradeData.BuyPrice, instrument.Ticker);
                                _stats.Update(instrument.Ticker, tradeData.BuyPrice, price);
                            }
                            break;
                    }
                }
            }
        }

        private async Task<bool> IsOrderExecuted(string figi, string orderId)
        {
            if (_settings.FakeConnection)
            {
                var tradeData = _tradeData[figi];
                var candle = _candles[figi].Candles[_candles[figi].Candles.Count - 1];
                if (tradeData.Status == Status.BuyPending)
                    return candle.Close <= tradeData.BuyPrice;
                else if (tradeData.Status == Status.SellPending)
                    return candle.Close >= tradeData.SellPrice;
                else
                    throw new Exception("Wrong trade data status on checking order execution!");

                return false;
            }
            else
            {
                var orders = await _context.OrdersAsync();
                foreach (var order in orders)
                {
                    if (order.OrderId == orderId)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        protected override void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                base.OnStreamingEventReceived(s, e);
                lock (_quotesLock)
                {
                    if (!_newQuotes.TryAdd(((CandleResponse)e.Response).Payload.Figi, 1))
                        _newQuotes[((CandleResponse)e.Response).Payload.Figi]++;
                }

                _quoteReceivedEvent.Set();
            }
        }

        public void TradeByHistory(string folderPath, string tickers = "")
        {
            if (!Directory.Exists(folderPath))
            {
                Logger.Write("Directory does not exists: {0}", folderPath);
                return;
            }

            if (tickers == null)
                tickers = "";

            var filter = tickers.Split(",", StringSplitOptions.RemoveEmptyEntries);

            int totalCandles = 0;

            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                if (filter.Length > 0 && Array.FindIndex(filter, x => x == ticker) == -1)
                    continue;

                string fileName = "";
                DirectoryInfo folder = new DirectoryInfo(folderPath);
                var files = folder.GetFiles(ticker + "_*.csv");
                if (files.Length == 1)
                    fileName = files[0].FullName;
                else
                {
                    files = folder.GetFiles(ticker + ".json");
                    if (files.Length == 1)
                        fileName = files[0].FullName;
                }

                if (fileName.Length > 0)
                {
                    // read history candles
                    var candleList = ReadCandles(files[0].FullName);
                    totalCandles += candleList.Count;
                    foreach (var candle in candleList)
                    {
                        OnStreamingEventReceived(this, new StreamingEventReceivedEventArgs(new CandleResponse(candle, DateTime.Now)));
                        _quoteProcessedEvent.WaitOne();
                    }
                }
            }

            Logger.Write("Total candles read: {0}", totalCandles);
        }

        public TradeStatistic TradeByHistory(List<CandlePayload> candleList)
        {
            _isShutingDown = false;
            InitCandles();
            _stats = new TradeStatistic();
            foreach (var candle in candleList)
            {
                OnStreamingEventReceived(this, new StreamingEventReceivedEventArgs(new CandleResponse(candle, DateTime.Now)));
                _quoteProcessedEvent.WaitOne();
            }

            CloseAll().Wait();

            return _stats;
        }

        public void CreateCandlesStatistic(string folderPath)
        {
            // read candles for each instrument and create dayly statistic
            // results will save to a file

            List<Screener.Stat> openToClose = new List<Screener.Stat>();
            List<Screener.Stat> lowToHigh = new List<Screener.Stat>();

            DirectoryInfo folder = new DirectoryInfo(folderPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var candleList = ReadCandles(f.FullName);
                if (candleList.Count > 0)
                {
                    decimal open = candleList[0].Open;
                    decimal close = candleList[0].Close;
                    decimal low = candleList[0].Low;
                    decimal high = candleList[0].High;

                    for (int i = 1; i < candleList.Count; ++i)
                    {
                        low = Math.Min(low, candleList[i].Low);
                        high = Math.Max(high, candleList[i].High);
                    }

                    close = candleList[candleList.Count - 1].Close;

                    var fileName = Path.GetFileNameWithoutExtension(f.FullName);
                    var fileParts = fileName.Split("_");
                    if (fileParts.Length < 3)
                        throw new Exception("Wrong file name format with candles");

                    var ticker = fileParts[0];
                    openToClose.Add(new Screener.Stat(ticker, open, close));

                    if (close >= open)
                        lowToHigh.Add(new Screener.Stat(ticker, low, high));
                    else
                        lowToHigh.Add(new Screener.Stat(ticker, high, low));
                }
            }

            openToClose.Sort();
            lowToHigh.Sort();

            // create output file
            var file = new StreamWriter(folderPath + "\\_stat.txt", false);
            file.AutoFlush = true;

            const int cMaxOutput = 15;
            Action<string, List<Screener.Stat>> showStat = (message, list) =>
            {
                file.WriteLine(message);
                var j = list.Count - 1;
                for (var i = 0; i < Math.Min(list.Count, cMaxOutput); ++i)
                {
                    String lOutput = String.Format("{0}:", list[i].ticker).PadLeft(7);
                    lOutput += String.Format("{0}%", list[i].change).PadLeft(7);
                    lOutput += String.Format("({0} ->> {1})", list[i].open, list[i].close).PadLeft(20);
                    lOutput = lOutput.PadRight(10);

                    file.Write(lOutput);

                    if (j >= cMaxOutput)
                    {
                        String rOutput = String.Format("{0}:", list[j].ticker).PadLeft(7);
                        rOutput += String.Format("{0}%", list[j].change).PadLeft(7);
                        rOutput += String.Format("({0} ->> {1})", list[j].open, list[j].close).PadLeft(20);

                        file.Write(rOutput);
                        --j;
                    }

                    file.Write("\n");
                }
            };

            showStat("Open to Close statistics:", openToClose);
            showStat("Low to High statistics:", lowToHigh);

            file.Close();
        }

        static public List<CandlePayload> ReadCandles(string filePath)
        {
            List<CandlePayload> result = new List<CandlePayload>();

            if (File.Exists(filePath))
            {
                if (Path.GetExtension(filePath) == ".json")
                {
                    var fileStream = File.OpenRead(filePath);
                    var streamReader = new StreamReader(fileStream);
                    String line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        result.Add(JsonConvert.DeserializeObject<CandlePayload>(line));
                    }
                }
                else if (Path.GetExtension(filePath) == ".csv")
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var fileParts = fileName.Split("_");
                    if (fileParts.Length < 3)
                        throw new Exception("Wrong file name format with candles");

                    var fileStream = File.OpenRead(filePath);
                    var streamReader = new StreamReader(fileStream);

                    var line = streamReader.ReadLine(); // skip header
                    var values = line.Split(";");
                    if (values.Length < 7)
                        throw new Exception("Wrong file format with candles");

                    string prevTime = "";
                    CandlePayload candle = null;

                    while ((line = streamReader.ReadLine()) != null)
                    {
                        values = line.Split(";");

                        if (candle == null)
                        {
                            candle = new CandlePayload();
                            candle.Figi = fileParts[1];
                            candle.Interval = CandleInterval.FiveMinutes;
                        }

                        if (prevTime != values[1])
                        {
                            // new candle
                            var time = fileParts[2] + " " + values[1];
                            candle.Time = DateTime.Parse(time);
                            candle.Time = DateTime.SpecifyKind(candle.Time, DateTimeKind.Utc);

                            prevTime = values[1];
                        }

                        // update candle
                        candle.Open = Helpers.Parse(values[2]);
                        candle.Close= Helpers.Parse(values[3]);
                        candle.Low= Helpers.Parse(values[4]);
                        candle.High = Helpers.Parse(values[5]);
                        candle.Volume = Helpers.Parse(values[6]);

                        result.Add(new CandlePayload(candle.Open, candle.Close, candle.High, candle.Low, candle.Volume, candle.Time, candle.Interval, candle.Figi));
                    }
                }
            }

            return result;
        }
    }
}