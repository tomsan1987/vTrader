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
        private IStrategy[] _strategies;
        private DateTime _lastTimeOut = DateTime.UtcNow.AddMinutes(-1);

        private List<PlacedLimitOrder> _orders = new List<PlacedLimitOrder>(); // orders created by Bot
        private List<Order> _portfolioOrders = new List<Order>(); // orders got from API
        private List<Portfolio.Position> _portfolio = new List<Portfolio.Position>(); // active portfolio
        private DateTime _lastPortfolioStatusQuery = DateTime.UtcNow;

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
                        case "GoodGrowStrategy":        _strategies[i] = new GoodGrowStrategy(); break;
                        case "ImpulseStrategy":         _strategies[i] = new ImpulseStrategy(); break;
                        case "MorningOpenStrategy":     _strategies[i] = new MorningOpenStrategy(); break;
                        default: Logger.Write("Unknown strategy name '{0}'", strategyNames[i]); break;
                    }
                }
            }
            else
            {
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

            // make a list of Figis which we will not trade due to it exist in portfolio
            {
                List<string> disabledFigis = null;
                string disabledTickers = "";

                if (!_settings.FakeConnection)
                {
                    disabledFigis = new List<string>();
                    var positions = _context.PortfolioAsync(_accountId).Result.Positions;
                    foreach (var it in positions)
                    {
                        disabledFigis.Add(it.Figi);
                        disabledTickers += it.Ticker;
                        disabledTickers += ";";
                    }
                }

                foreach (var ticker in _watchList)
                {
                    var figi = _tickerToFigi[ticker];
                    var tradeData = new TradeData();

                    if (disabledFigis != null && disabledFigis.Exists(x => x.Contains(figi)))
                        tradeData.DisabledTrading = true;

                    _tradeData.Add(figi, tradeData);
                }

                Logger.Write("List of disabled tickers: " + disabledTickers);
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

                if (tradeData.DisabledTrading)
                    return;

                await UpdateOrdersStatus(figi);

                if (_candles[figi].IsSpike())
                {
                    _candles[figi].SpikePositions.Add(_candles[figi].Raw.Count - 1);
                    Logger.Write("{0}: SPYKE!. {1}", instrument.Ticker, Helpers.CandleDesc(_candles[figi].Raw.Count - 1, candle));
                    return;
                }

                if (_settings.SubscribeQuotes)
                {
                    // make sure that we got actual candle
                    TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.AddMinutes(-5).ToUniversalTime().Ticks - candle.Time.Ticks);
                    if (elapsedSpan.TotalSeconds > 5)
                        return;
                }

                LimitOrder order = null;
                var operation = IStrategy.StrategyResultType.NoOp;

                if (tradeData.Strategy == null)
                {
                    foreach (var strategy in _strategies)
                    {
                        if (strategy != null)
                        {
                            operation = strategy.Process(instrument, tradeData, _candles[figi], out order);
                            if (operation != IStrategy.StrategyResultType.NoOp)
                            {
                                tradeData.Strategy = strategy;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    operation = tradeData.Strategy.Process(instrument, tradeData, _candles[figi], out order);
                }

                // process Strategy's result
                if (operation == IStrategy.StrategyResultType.Buy || operation == IStrategy.StrategyResultType.Sell)
                {
                    order.BrokerAccountId = _accountId;
                    var placedOrder = await _context.PlaceLimitOrderAsync(order);
                    placedOrder.Figi = instrument.Figi;
                    placedOrder.Price = order.Price;

                    if (placedOrder.Status == OrderStatus.Cancelled || placedOrder.Status == OrderStatus.PendingCancel || placedOrder.Status == OrderStatus.Rejected || placedOrder.RejectReason?.Length > 0)
                    {
                        Logger.Write("{0}: OrderId: {1}. Lost: {2}. Unsuccessful! Status: {3}. RejectReason: {4}",
                            instrument.Ticker, placedOrder.OrderId, placedOrder.RequestedLots, placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                    else
                    {
                        _orders.Add(placedOrder);

                        tradeData.Status = (operation == IStrategy.StrategyResultType.Buy) ? Status.BuyPending : Status.SellPending;
                        tradeData.Time = candle.Time;
                        tradeData.BuyTime = candle.Time;
                        tradeData.CandleID = _candles[figi].Raw.Count;
                        Logger.Write("{0}: OrderId: {1}. Lost: {2}. PlacedOrder. Status: {3}. RejectReason: {4}",
                            instrument.Ticker, placedOrder.OrderId, placedOrder.RequestedLots, placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                }
                else if (operation == IStrategy.StrategyResultType.CancelOrder)
                {
                    if (_settings.FakeConnection)
                    {
                        // just remove order
                        _orders.RemoveAll(x => x.Figi == instrument.Figi);
                        if (tradeData.Status == Status.BuyPending)
                        {
                            tradeData.Reset(true);
                        }
                    }
                    else
                    {
                        // try to cancel order
                        // TODO: situation: we canceling order, but it may be partially executed
                        foreach (var it in _orders)
                        {
                            if (it.Figi == instrument.Figi)
                            {
                                bool successed = false;
                                try
                                {
                                    await _context.CancelOrderAsync(it.OrderId, _accountId);
                                    successed = true;
                                }
                                catch (OpenApiException e)
                                {
                                    Logger.Write("OpenApiException while cancel order: " + e.Message);
                                    // may be executed.... TODO
                                }

                                if (successed)
                                    it.OrderId = "";
                            }
                        }

                        _orders.RemoveAll(x => x.OrderId == "");
                    }
                }
            }
            catch (OpenApiException e)
            {
                Logger.Write("OpenApiException: " + e.Message);

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

        private async Task UpdateOrdersStatus(string figi, bool force = false)
        {
            if (_settings.FakeConnection)
            {
                UpdatePortfolioFakeConnection(figi);
            }
            else if (force || (DateTime.UtcNow - _lastPortfolioStatusQuery).TotalSeconds > 20)
            {
                // we need to query active orders and portfolio from API once per 20 second

                if (_orders.Count > 0 || _portfolioOrders.Count > 0 || _portfolio.Count > 0)
                {
                    Logger.Write("Updating portfolio status... ");

                    _portfolioOrders = await _context.OrdersAsync(_accountId);
                    _portfolio = _context.PortfolioAsync(_accountId).Result.Positions;
                    _lastPortfolioStatusQuery = DateTime.UtcNow;

                    // erase elements for disabled instruments
                    _portfolioOrders.RemoveAll(x => !_tradeData.ContainsKey(x.Figi) || _tradeData[x.Figi].DisabledTrading);
                    _portfolio.RemoveAll(x => !_tradeData.ContainsKey(x.Figi) || _tradeData[x.Figi].DisabledTrading);

                    LogPortfolioAndOrders();
                }
            }

            // lets look what orders was executed
            foreach (var it in _orders)
            {
                var instrument = _figiToInstrument[it.Figi];
                int executedLots = 0;

                var idx = _portfolioOrders.FindIndex(x => x.OrderId == it.OrderId);
                if (idx >= 0)
                {
                    // check if order partially executed
                    if (_portfolioOrders[idx].RequestedLots > _portfolioOrders[idx].ExecutedLots && it.ExecutedLots < _portfolioOrders[idx].ExecutedLots)
                    {
                        // partially executed, check how lots added to portfolio
                        executedLots = _portfolioOrders[idx].ExecutedLots - it.ExecutedLots;
                        var prevLots = _tradeData[it.Figi].Lots;
                        var portfolioLots = GetLotsInPortfolio(it.Figi);
                        if (Math.Abs(portfolioLots - prevLots) == executedLots)
                        {
                            it.ExecutedLots = it.ExecutedLots + executedLots;
                            _tradeData[it.Figi].Update(it.Operation, executedLots, it.Price, it.OrderId);
                            _tradeData[it.Figi].CandleID = _candles[it.Figi].Raw.Count;
                            _tradeData[it.Figi].Time = _candles[figi].Candles[_candles[figi].Candles.Count - 1].Time;

                            Logger.Write("{0}: OrderID: {1} partially executed. Executed lots: {2}. OrderStatus: {3}/{4} lots",
                                _figiToTicker[it.Figi], it.OrderId, executedLots, _portfolioOrders[idx].ExecutedLots, _portfolioOrders[idx].RequestedLots);
                        }
                        else
                        {
                            Logger.Write("{0}: OrderID: {1}. Partially executed, but not added to Portfolio. PlacedOrder: {2}/{3}. PortfolioOrder: {4}/{5}",
                                _figiToTicker[it.Figi], it.OrderId, it.ExecutedLots, it.RequestedLots, _portfolioOrders[idx].ExecutedLots, _portfolioOrders[idx].RequestedLots);
                        }
                    }
                }
                else
                {
                    // seems executed if added to portfolio
                    executedLots = it.RequestedLots - it.ExecutedLots;
                    var prevLots = _tradeData[it.Figi].Lots;
                    var portfolioLots = GetLotsInPortfolio(it.Figi);
                    if (Math.Abs(portfolioLots - prevLots) == executedLots)
                    {
                        // order executed, added to portfolio! remove from orders
                        it.ExecutedLots = it.ExecutedLots + executedLots;
                        _tradeData[it.Figi].Update(it.Operation, executedLots, it.Price, it.OrderId);
                        _tradeData[it.Figi].CandleID = _candles[it.Figi].Raw.Count;
                        _tradeData[it.Figi].Time = _candles[figi].Candles[_candles[figi].Candles.Count - 1].Time;

                        if (it.RequestedLots == it.ExecutedLots)
                        {
                            Logger.Write("{0}: OrderID: {1} fully executed", instrument.Ticker, it.OrderId);
                            it.OrderId = "";
                        }

                        if (_tradeData[it.Figi].Lots == 0)
                        {
                            // trade finished
                            _stats.Update(instrument.Ticker, _tradeData[it.Figi].AvgPrice, _tradeData[it.Figi].AvgSellPrice, _tradeData[it.Figi].GetOrdersInLastTrade(), _tradeData[it.Figi].GetLotsInLastTrade());
                            _tradeData[it.Figi].Reset(false);

                            // cancel all remaining orders by this instrument
                            foreach (var o in _orders)
                            {
                                if (o.Figi == instrument.Figi && o.OrderId != it.OrderId)
                                    o.Status = OrderStatus.PendingCancel;
                            }
                        }
                    }
                    else
                    {
                        Logger.Write("{0}: OrderID: {1}. Waiting execution... Not found in portfolio orders, but not added to Portfolio. PlacedOrder: {2}/{3}.",
                            _figiToTicker[it.Figi], it.OrderId, it.ExecutedLots, it.RequestedLots);
                    }
                }
            }

            // look for canceled orders
            foreach (var it in _orders)
            {
                if (it.Status == OrderStatus.PendingCancel)
                {
                    // cancel order
                    if (CancelOrder(it.OrderId))
                        it.OrderId = "";
                }
            }

            // erase orders with empty orderID
            _orders.RemoveAll(x => x.OrderId.Length == 0);
        }

        private void LogPortfolioAndOrders()
        {
            // Log active orders
            if (_portfolioOrders.Count > 0)
            {
                Logger.Write("Active orders: {0}", _portfolioOrders.Count);
                foreach (var it in _portfolioOrders)
                {
                    if (!_tradeData.ContainsKey(it.Figi) || _tradeData[it.Figi].DisabledTrading)
                        continue;

                    Logger.Write("   {0}: {1}. {2}/{3}", _figiToInstrument[it.Figi].Ticker, it.Operation, it.ExecutedLots, it.RequestedLots);
                }
            }

            // Log portfolio
            if (_portfolio.Count > 0)
            {
                Logger.Write("Portfolio: {0}", _portfolio.Count);
                foreach (var it in _portfolio)
                {
                    if (!_tradeData.ContainsKey(it.Figi) || _tradeData[it.Figi].DisabledTrading)
                        continue;

                    Logger.Write("   {0}: {1}", _figiToInstrument[it.Figi].Ticker, it.Lots);
                }
            }
        }

        private bool CancelOrder(string orderID)
        {
            bool result = false;
            string message = "";
            if (_settings.FakeConnection)
            {
                // just return true
                result = true;
            }
            else
            {
                try
                {
                    _context.CancelOrderAsync(orderID, _accountId).Wait();
                    result = true;
                }
                catch (OpenApiException e)
                {
                    message = "OpenApiException while cancel order: " + e.Message;
                }
            }

            if (result)
                Logger.Write("OrderID: {0} successfully canceled", orderID);
            else
                Logger.Write("OrderID: {0} unable to cancel: {1}", orderID, message);

            return result;
        }

        private void UpdatePortfolioFakeConnection(string figi)
        {
            if (_settings.FakeConnection)
            {
                foreach (var it in _orders)
                {
                    if (it.Figi == figi)
                    {
                        var instrument = _figiToInstrument[figi];
                        var tradeData = _tradeData[figi];
                        var rawData = _candles[figi].Raw;
                        var requestedLots = it.RequestedLots - it.ExecutedLots;

                        // we need to add order to portfolio orders if did not do before
                        var idxPortfolioOrder = _portfolioOrders.FindIndex(x => x.OrderId == it.OrderId);
                        if (it.ExecutedLots == 0 && idxPortfolioOrder < 0)
                        {
                            _portfolioOrders.Add(new Order(it.OrderId, it.Figi, it.Operation, OrderStatus.New, it.RequestedLots, 0, OrderType.Limit, it.Price));
                            idxPortfolioOrder = _portfolioOrders.FindIndex(x => x.OrderId == it.OrderId);
                        }

                        int executedLots = 0;
                        for (int i = tradeData.CandleID; i < rawData.Count; ++i)
                        {
                            if (it.Operation == OperationType.Buy && rawData[i].Price <= it.Price)
                                executedLots += (int)rawData[i].Volume;
                            else if (it.Operation == OperationType.Sell && rawData[i].Price >= it.Price)
                                executedLots += (int)rawData[i].Volume;
                        }

                        executedLots = Math.Min(executedLots, requestedLots);
                        if (executedLots > 0)
                        {
                            _portfolioOrders[idxPortfolioOrder].ExecutedLots += executedLots;
                            tradeData.CandleID = rawData.Count;

                            if (_portfolioOrders[idxPortfolioOrder].ExecutedLots == _portfolioOrders[idxPortfolioOrder].RequestedLots)
                                _portfolioOrders.RemoveAll(x => x.OrderId == it.OrderId);

                            // add lots to portfolio
                            var idx = _portfolio.FindIndex(x => x.Figi == it.Figi);
                            if (idx >= 0)
                            {
                                if (it.Operation == OperationType.Buy)
                                    _portfolio[idx].Lots += executedLots;
                                else
                                    _portfolio[idx].Lots -= executedLots;

                                if (_portfolio[idx].Lots == 0)
                                {
                                    // remove from portfolio
                                    _portfolio.RemoveAll(x => x.Figi == it.Figi);
                                }
                            }
                            else
                                _portfolio.Add(new Portfolio.Position(instrument.Name, figi, instrument.Ticker, "", InstrumentType.Stock, 0, 0, null, executedLots, null, null));
                        }
                    }
                }
            }
        }

        private int GetLotsInPortfolio(string figi)
        {
            int lots = 0;
            var idx = _portfolio.FindIndex(x => x.Figi == figi);
            if (idx >= 0)
                lots = _portfolio[idx].Lots;

            return lots;
        }

        private async Task CloseAll()
        {
            _isShutingDown = true;
            await UpdateOrdersStatus("", true);

            foreach (var it in _tradeData)
            {
                var tradeData = it.Value;
                if (tradeData != null && tradeData.AvgPrice != 0)
                {
                    var candle = _candles[it.Key].Candles[_candles[it.Key].Candles.Count - 1];
                    switch (tradeData.Status)
                    {
                        case Status.BuyPending:
                            {
                                var instrument = _figiToInstrument[it.Key];
                                //if (await IsOrderExecuted(instrument.Ticker, it.Key, tradeData.OrderId))
                                //{
                                //    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                                //    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price, _accountId);
                                //    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                                //    Logger.Write("{0}: Closing dayly orders. Close price: {1}. {2}. Profit: {3}({4}%)",
                                //        instrument.Ticker, price, Helpers.CandleDesc(_candles[it.Key].Raw.Count - 1, candle), price - tradeData.AvgPrice, Helpers.GetChangeInPercent(tradeData.AvgPrice, price));

                                //    _stats.Sell(tradeData.BuyTime, candle.Time, tradeData.AvgPrice, instrument.Ticker);
                                //    _stats.Update(instrument.Ticker, tradeData.AvgPrice, price);
                                //}
                                //else
                                //{
                                //    // just cancel order
                                //    Logger.Write("{0}: Cancel order. {0}. Details: end of day", instrument.Ticker, Helpers.CandleDesc(_candles[it.Key].Raw.Count - 1, candle));
                                //    await _context.CancelOrderAsync(tradeData.OrderId, _accountId);
                                //}
                                // TODO
                            }
                            break;

                        case Status.BuyDone:
                            {
                                var instrument = _figiToInstrument[it.Key];
                                var price = candle.Close - 2 * instrument.MinPriceIncrement;
                                var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price, _accountId);
                                var placedOrder = await _context.PlaceLimitOrderAsync(order);

                                Logger.Write("{0}: Closing dayly orders. Close price: {1}. {2}. Profit: {3}({4}%)",
                                    instrument.Ticker, price, Helpers.CandleDesc(_candles[it.Key].Raw.Count - 1, candle), price - tradeData.AvgPrice, Helpers.GetChangeInPercent(tradeData.AvgPrice, price));

                                _stats.Sell(tradeData.BuyTime, candle.Time, tradeData.AvgPrice, instrument.Ticker);
                                _stats.Update(instrument.Ticker, tradeData.AvgPrice, tradeData.AvgSellPrice, tradeData.GetOrdersInLastTrade(), tradeData.GetLotsInLastTrade());
                            }
                            break;
                    }
                }
            }
        }

        private async Task<bool> IsOrderExecuted(string ticker, string figi, string orderId)
        {
            var tradeData = _tradeData[figi];
            var rawData = _candles[figi].Raw;

            if (tradeData.Status != Status.BuyPending && tradeData.Status != Status.SellPending)
                throw new Exception("Wrong trade data status on checking order execution!");

            bool maybeExecuted = false;
            for (int i = tradeData.CandleID; i < rawData.Count; ++i)
            {
                if (tradeData.Status == Status.BuyPending && rawData[i].Price <= tradeData.AvgPrice)
                    maybeExecuted = true;
                //else if (tradeData.Status == Status.SellPending && rawData[i].Price >= tradeData.SellPrice)
                //    maybeExecuted = true;
            }

            if (maybeExecuted)
            {
                // when test mode and price reached - will think that order was executed
                if (_settings.FakeConnection)
                    return true;

                Logger.Write("{0}: OrderID: {1} may be executed", ticker, orderId);

                //if (_candles[figi].Raw.Count < tradeData.CandleID + 5)
                //    return false;

                // price was reached, we need to make sure that instrument added to portfolio and order was executed
                bool foundInOrderList = false;
                var orders = await _context.OrdersAsync(_accountId);
                foreach (var order in orders)
                {
                    if (order.OrderId == orderId)
                    {
                        foundInOrderList = true;
                        break;
                    }
                }

                // check the portfolio
                bool foundInPortfolio = false;
                if (!foundInOrderList)
                {
                    Logger.Write("{0}: OrderID: {1} not found in order list", ticker, orderId);

                    var positions = _context.PortfolioAsync(_accountId).Result.Positions;
                    foreach (var it in positions)
                    {
                        if (it.Figi == figi)
                        {
                            foundInPortfolio = true;
                            break;
                        }
                    }
                }

                var fullyExecuted = !foundInOrderList;
                fullyExecuted &= (tradeData.Status == Status.BuyPending && foundInPortfolio) || (tradeData.Status == Status.SellPending && !foundInPortfolio);

                return fullyExecuted;
            }

            return false;
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

        public Dictionary<string, TradeStatistic> TradeByHistory(string candlesPath, string outputFolder, string tickers = "")
        {
            if (!Directory.Exists(candlesPath))
            {
                Logger.Write("Directory does not exists: {0}", candlesPath);
                return null;
            }

            if (tickers == null)
                tickers = "";

            Dictionary<string, TradeStatistic> globalStat = new Dictionary<string, TradeStatistic>();

            var filter = tickers.Split(",", StringSplitOptions.RemoveEmptyEntries);

            int totalCandles = 0;
            TradeStatistic statPrev = (TradeStatistic)_stats.Clone();

            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                if (filter.Length > 0 && Array.FindIndex(filter, x => x == ticker) == -1)
                    continue;

                DirectoryInfo folder = new DirectoryInfo(candlesPath);
                var files = folder.GetFiles(ticker + "_*.csv", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    {
                        // reset some states members
                        _isShutingDown = false;
                        InitCandles();
                        foreach (var it in _tradeData)
                            it.Value.Reset(true);
                    }

                    var fileName = file.Name.Substring(0, file.Name.LastIndexOf("."));
                    Logger.Write(fileName);

                    // read history candles
                    var candleList = ReadCandles(file.FullName, figi);
                    totalCandles += candleList.Count;
                    foreach (var candle in candleList)
                    {
                        base.OnStreamingEventReceived(this, new StreamingEventReceivedEventArgs(new CandleResponse(candle, DateTime.Now)));
                        OnCandleUpdate(candle.Figi).Wait();
                    }

                    // final clean up
                    CloseAll().Wait();

                    if (statPrev.totalOrders < _stats.totalOrders)
                    {
                        TradeStatistic currentStat = new TradeStatistic();
                        currentStat.totalOrders = _stats.totalOrders - statPrev.totalOrders;
                        currentStat.posOrders = _stats.posOrders - statPrev.posOrders;
                        currentStat.negOrders= _stats.negOrders- statPrev.negOrders;
                        currentStat.volume = _stats.volume - statPrev.volume;
                        currentStat.comission = _stats.comission - statPrev.comission;
                        currentStat.totalProfit = _stats.totalProfit - statPrev.totalProfit;
                        globalStat.Add(fileName, currentStat);


                        if (outputFolder?.Length > 0)
                        {
                            // copy candles to output folder
                            try
                            {
                                File.Copy(file.FullName, outputFolder + "\\" + file.Name);
                            }
                            catch (Exception e)
                            {
                                Logger.Write("Exception happened while copiyng file. Error: " + e.Message);
                            }
                        }

                        statPrev = (TradeStatistic)_stats.Clone();
                    }
                }
            }

            Logger.Write("Total candles read: {0}", totalCandles);

            return globalStat;
        }

        public TradeStatistic TradeByHistory(List<CandlePayload> candleList)
        {
            _isShutingDown = false;
            InitCandles();
            _stats = new TradeStatistic();

            foreach (var it in _tradeData)
                it.Value.Reset(true);

            foreach (var candle in candleList)
            {
                base.OnStreamingEventReceived(this, new StreamingEventReceivedEventArgs(new CandleResponse(candle, DateTime.Now)));
                OnCandleUpdate(candle.Figi).Wait();
            }

            CloseAll().Wait();

            return _stats;
        }

        // actualFigi - it is possible that figi has been changed for ticker. 
        // In this case we will read history data and initialize it with incorrect figi. Pass actual figi for instument if need to have correct figi for history data.
        static public List<CandlePayload> ReadCandles(string filePath, string actualFigi = "")
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

                    var figi = fileParts[1];
                    if (actualFigi.Length > 0 && figi != actualFigi)
                    {
                        Logger.Write("Warning: figi `{0}` for history data does not match actual figi `{1}`. Ticker: {2}. Will be used actual!", figi, actualFigi, fileParts[0]);
                        figi = actualFigi;
                    }

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
                            candle.Figi = figi;
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