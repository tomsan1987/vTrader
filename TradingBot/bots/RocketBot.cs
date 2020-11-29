using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
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
        private Dictionary<string, TradeData> _tradeData = new Dictionary<string, TradeData>();
        private TradeStatistic _stats = new TradeStatistic();
        private bool _isShutingDown = false;

        public RocketBot(string token, string configPath) : base(token, configPath)
        {
            Logger.Write("Rocket bot created");
        }

        public override async ValueTask DisposeAsync()
        {
            await CloseAll();

            String message = "Orders statistic:";
            foreach (var it in _stats.logMessages)
            {
                message += "\n";
                message += it;
            }
            Logger.Write(message);

            Logger.Write("Trade statistic. Total/Pos/Neg {0}/{1}/{2}. Profit: {3}. Volume: {4}. MaxVolume: {5}. Comission: {6}", _stats.totalOrders, _stats.posOrders, _stats.negOrders, _stats.totalProfit, _stats.volume, _stats.maxVolume, _stats.comission);
            await Task.Yield();
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            foreach (var ticker in _watchList)
            {
                var figi = _tickerToFigi[ticker];
                _tradeData.Add(_tickerToFigi[ticker], new TradeData());
            }

            //await SubscribeCandles();
        }

        public override void ShowStatus()
        {
            Console.Clear();
            Screener.ShowStatusStatic(_figiToTicker, _lastCandleReceived, _candles);
        }

        private async Task OnCandleUpdate(string figi)
        {
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

            //if (candle.Close < 100)
            //    return;

            if (tradeData.Status == Status.Watching)
            {
                // make sure that we got actual candle
                //TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.AddMinutes(-5).ToUniversalTime().Ticks - candle.Time.Ticks);
                //if (elapsedSpan.TotalSeconds > 5)
                //    return;


                decimal volume = 0;
                for (int i = Math.Max(0, candles.Count - 3); i < candles.Count; ++i)
                {
                    if (candles[i].Volume < 10)
                        volume = decimal.MinValue;

                    volume += candles[i].Volume;
                }

                if (volume > 1000)
                {
                    decimal[] sConditions = new decimal[] {3m, 2m};

                    bool posGrow = false;
                    string reason = "";
                    for (int i = 0; i < sConditions.Length && !posGrow; ++i)
                    {
                        int candlesRequired = i + 1;

                        int start = candles.Count - candlesRequired;
                        if (start > 0)
                        {
                            var change = Helpers.GetChangeInPercent(candles[start].Open, candle.Close);
                            if (change >= sConditions[i])
                            {
                                // check that it is not grow after fall
                                int timeAgoStart = Math.Max(0, start - 24);
                                var localChange = Helpers.GetChangeInPercent(candles[timeAgoStart].Close, candles[start - 1].Close);
                                if (localChange > 0 || 2 * Math.Abs(localChange) < change)
                                {
                                    posGrow = true;
                                    reason = String.Format("{0} candles, grow +{1}%, local change {2}%", candlesRequired, change, localChange);
                                }
                            }
                        }

                        //int postiveCandles = 0;
                        //for (int k = Math.Max(0, candles.Count - candlesRequired); k < candles.Count; ++k)
                        //{
                        //    if (Helpers.GetChangeInPercent(candles[k]) >= sConditions[i])
                        //        ++postiveCandles;
                        //}

                        //if (postiveCandles == candlesRequired)
                        //{
                        //    posGrow = true;
                        //    reason = String.Format("{0} candles, +{1}%", postiveCandles, sConditions[i]);
                        //}
                    }

                    //decimal momentumChange = Helpers.GetChangeInPercent(candle.Low, candle.Close);
                    //if (candles.Count > 1)
                    //    momentumChange = Math.Max(momentumChange, Helpers.GetChangeInPercent(candles[candles.Count - 1].Low, candle.Close));

                    // checked that it is not a grow after a fall
                    //decimal localChange = 0;
                    //int j = Math.Max(0, candles.Count - 13);
                    //if (candles.Count - 3 > 0)
                    //    localChange = Helpers.GetChangeInPercent(candles[j].Close, candles[candles.Count - 3].Close);

                    // try to buy if it down well


                    if (/*localChange >= (decimal)-0.2 &&*/ posGrow)
                    {
                        // buy 1 lot
                        var price = candle.Close + instrument.MinPriceIncrement;
                        var order = new LimitOrder(instrument.Figi, 1, OperationType.Buy, price);
                        var placedOrder = await _context.PlaceLimitOrderAsync(order);

                        tradeData.OrderId = placedOrder.OrderId;
                        tradeData.BuyPrice = price;
                        tradeData.StopPrice = Helpers.RoundPrice(tradeData.BuyPrice * (decimal)0.98, instrument.MinPriceIncrement);
                        tradeData.Status = Status.BuyPending;
                        tradeData.Time = candle.Time;
                        _stats.Buy(tradeData.BuyPrice);

                        Logger.Write("{0}: Buy. Price: {1}. StopPrice: {2}. Candle: {3}. Details: Reason: {4}, Status - {5}, RejectReason -  {6}",
                            instrument.Ticker, tradeData.BuyPrice, tradeData.StopPrice, JsonConvert.SerializeObject(candle), reason, placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                    //else if (momentumChange >= (decimal)1.5 && (localChange >= (decimal)-0.2 || momentumChange >= 2 * Math.Abs(localChange)))
                    //{
                    //    // buy 1 lot
                    //    var price = candle.Close + 2 * instrument.MinPriceIncrement;
                    //    var order = new LimitOrder(instrument.Figi, 1, OperationType.Buy, price);
                    //    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                    //    tradeData.OrderId = placedOrder.OrderId;
                    //    tradeData.BuyPrice = price;
                    //    tradeData.StopPrice = Helpers.RoundPrice(tradeData.BuyPrice * (decimal)0.98, instrument.MinPriceIncrement);
                    //    tradeData.Status = Status.BuyPending;
                    //    tradeData.Time = candle.Time;
                    //    _stats.Buy(tradeData.BuyPrice);

                    //    Logger.Write("{0}: Buy. BuyPrice: {1}. StopPrice: {2}. Candle: {3}. Details: Reason: 'momentumChange', Status - {4}, RejectReason -  {5}",
                    //        instrument.Ticker, tradeData.BuyPrice, tradeData.StopPrice, JsonConvert.SerializeObject(candle), placedOrder.Status.ToString(), placedOrder.RejectReason);
                    //}
                }
            }
            else if (tradeData.Status == Status.BuyPending)
            {
                // check if limited order executed
                
                if (await IsOrderExecuted(tradeData.OrderId))
                {
                    // order executed
                    tradeData.Status = Status.BuyDone;
                    tradeData.Time = candle.Time;
                }
                else
                {
                    // check if price changed is not significantly
                    var change = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                    if (change >= (decimal)0.5)
                    {
                        Logger.Write("{0}: Cancel. Candle: {1}. Details: price change {2}", instrument.Ticker, JsonConvert.SerializeObject(candle), change);

                        await _context.CancelOrderAsync(tradeData.OrderId);
                        tradeData.OrderId = null;
                        tradeData.BuyPrice = 0;
                    }
                }
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                // check if we reach stop conditions
                //if (candle.Close <= tradeData.StopPrice)
                //{
                //    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                //    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                //    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                //    Logger.Write("{0}: Sell by SL. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));
                //    tradeData.Status = Status.SellPending;
                //    tradeData.Time = candle.Time;
                //    _stats.Sell(tradeData.BuyPrice);

                //    _stats.Update(tradeData.BuyPrice, price);
                //}
                //else if (tradeData.StopPrice < tradeData.BuyPrice && Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close) >= (decimal)1.0)
                //{
                //    // move stop loss to no loss
                //    tradeData.StopPrice = tradeData.BuyPrice;//Helpers.RoundPrice(tradeData.BuyPrice * (decimal)1.005, instrument.MinPriceIncrement);
                //    tradeData.Time = candle.Time;
                //    Logger.Write("{0}: Moving stop loss to no loss. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                //}
                //else if (tradeData.StopPrice >= tradeData.BuyPrice && Helpers.GetChangeInPercent(tradeData.StopPrice, candle.Close) >= (decimal)2.0)
                //{
                //    // pulling the stop to the price
                //    tradeData.StopPrice = Helpers.RoundPrice(candle.Close * (decimal)0.99, instrument.MinPriceIncrement);
                //    tradeData.Time = candle.Time;
                //    Logger.Write("{0}: Pulling stop loss to current price. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                //}

                // check that it is not a Rocket!
                //var time = tradeData.Time.AddMinutes(10);
                //if (candle.Time == time)
                //{
                //    if (candles[candles.Count - 1].Close < tradeData.BuyPrice)
                //    {
                //        var price = candle.Close - 2 * instrument.MinPriceIncrement;
                //        var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                //        var placedOrder = await _context.PlaceLimitOrderAsync(order);

                //        Logger.Write("{0}: Sell by not Rocket reasons. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));
                //        tradeData.Status = Status.SellPending;
                //        tradeData.Time = candle.Time;
                //        _stats.Sell(tradeData.BuyPrice);

                //        _stats.Update(instrument.Ticker, tradeData.BuyPrice, price);
                //    }
                //}


                TimeSpan elapsedTime = new TimeSpan(DateTime.Now.ToUniversalTime().Ticks - tradeData.Time.Ticks);
                if (elapsedTime.TotalMinutes > 25)
                {
                    //if (tradeData.BuyPrice > candle.Close)
                    //{
                    //    // close order forcibly
                    //    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                    //    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                    //    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                    //    Logger.Write("{0}: Sell by Timeout. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));
                    //    tradeData.Status = Status.SellPending;
                    //    tradeData.Time = candle.Time;
                    //    _stats.Sell(tradeData.BuyPrice);

                    //    _stats.Update(tradeData.BuyPrice, price);
                    //}
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
                                if (await IsOrderExecuted(tradeData.OrderId))
                                {
                                    var instrument = _figiToInstrument[it.Key];
                                    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                                    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                                    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                                    Logger.Write("{0}: Closing dayly orders. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), price - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, price));

                                    _stats.Sell(tradeData.BuyPrice);
                                    _stats.Update(instrument.Ticker, tradeData.BuyPrice, price);
                                }
                                else
                                {
                                    // just cancel order
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

                                _stats.Sell(tradeData.BuyPrice);
                                _stats.Update(instrument.Ticker, tradeData.BuyPrice, price);
                            }
                            break;
                    }
                }
            }
        }

        private async Task<bool> IsOrderExecuted(string orderId)
        {
            var orders = await _context.OrdersAsync();
            foreach (var order in orders)
            {
                if (order.OrderId == orderId)
                {
                    return false;
                }
            }

            return true;
        }

        protected override void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                base.OnStreamingEventReceived(s, e);
                OnCandleUpdate(((CandleResponse)e.Response).Payload.Figi);
            }
        }

        public async Task TradeByHistory(string folderPath, string tickets = "")
        {
            // unsubscribe from candles
            _context.StreamingEventReceived -= base.OnStreamingEventReceived;
            _context.WebSocketException -= base.OnWebSocketExceptionReceived;
            _context.StreamingClosed -= base.OnStreamingClosedReceived;
            foreach (var it in _candles)
            {
                it.Value.Candles.Clear();
            }

            var filter = tickets.Split(",", StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                if (filter.Length > 0 && Array.FindIndex(filter, x => x == ticker) == -1)
                    continue;

                // read history candles
                var filePath = folderPath + "\\" + ticker + ".csv";
                var candleList = ReadCandles(filePath);

                foreach (var candle in candleList)
                {
                    base.OnStreamingEventReceived(this, new StreamingEventReceivedEventArgs(new CandleResponse(candle, DateTime.Now)));
                    await OnCandleUpdate(candle.Figi);
                }
            }
        }

        private List<CandlePayload> ReadCandles(string filePath)
        {
            List<CandlePayload> result = new List<CandlePayload>();

            if (File.Exists(filePath))
            {
                var fileStream = File.OpenRead(filePath);
                var streamReader = new StreamReader(fileStream);
                String line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    result.Add(JsonConvert.DeserializeObject<CandlePayload>(line));
                }
            }

            return result;
        }
    }
}