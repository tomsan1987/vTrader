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
        private Dictionary<string, Quotes> _candles = new Dictionary<string, Quotes>();
        private Dictionary<string, TradeData> _tradeData = new Dictionary<string, TradeData>();
        private DateTime _lastCandleReceived;
        private TradeStatistic _stats = new TradeStatistic();


        public RocketBot(string token, string configPath) : base(token, configPath)
        {
            Logger.Write("Rocket bot created");
        }

        public override async ValueTask DisposeAsync()
        {
            //_status = Status.ShutDown;
            //_candleReceivedEvent.Set();
            //_tradeTask.Dispose();
            await Task.Yield();
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
            _context.StreamingEventReceived += OnStreamingEventReceived;
            _context.WebSocketException += OnWebSocketExceptionReceived;
            _context.StreamingClosed += OnStreamingClosedReceived;

            foreach (var ticker in _watchList)
            {
                var figi = _tickerToFigi[ticker];
                _tradeData.Add(_tickerToFigi[ticker], new TradeData());
            }

            await SubscribeCandles();
        }

        public override void ShowStatus()
        {
            Console.Clear();
            Console.WriteLine("Time of last quote received: {0}", _lastCandleReceived.ToString("HH:mm:ss"));
        }

        private async Task OnCandleUpdate(string figi)
        {
            var tradeData = _tradeData[figi];
            var instrument = _figiToInstrument[figi];
            var candles = _candles[figi].Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.Status == Status.Watching)
            {
                // conditions: 
                //      - quote is actual
                //      - last 3 candles should be positives and at least by 0.2% grow
                //      - momentum change: current price - min(current or previous candle) > 1%
                //      - volume

                // make sure that we got actual candle
                TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.AddMinutes(-5).ToUniversalTime().Ticks - candle.Time.Ticks);
                if (elapsedSpan.TotalSeconds > 5)
                    return;

                bool historyGrow = false;
                if (candles.Count > 3)
                {
                    int postiveCandles = 0;
                    for (int i = candles.Count - 3; i < candles.Count; ++i)
                    {
                        if (Helpers.GetChangeInPercent(candles[i]) >= (decimal)0.2)
                            ++postiveCandles;
                    }

                    decimal change = Helpers.GetChangeInPercent(candles[candles.Count - 3].Low, candle.Close);

                    historyGrow = (postiveCandles >= 3 && change > (decimal)0.5);
                }

                decimal momentumChange = Helpers.GetChangeInPercent(candle.Low, candle.Close);
                if (candles.Count > 1)
                    momentumChange = Math.Max(momentumChange, Helpers.GetChangeInPercent(candles[candles.Count - 1].Low, candle.Close));

                // checked that it is not a grow after a fall
                decimal localChange = 0;
                int j = Math.Max(0, candles.Count - 13);
                if (candles.Count - 3 > 0)
                    localChange = Helpers.GetChangeInPercent(candles[j].Close, candles[candles.Count - 3].Close);

                decimal volume = 0;
                for (int i = Math.Max(0, candles.Count - 3); i < candles.Count; ++i)
                {
                    if (candles[i].Volume < 1000)
                        volume = decimal.MinValue;

                    volume += candles[i].Volume;
                }

                if (volume > 1000)
                {
                    if (localChange >= 0 && historyGrow)
                    {
                        // buy 1 lot
                        var price = candle.Close + 2 * instrument.MinPriceIncrement;
                        var order = new LimitOrder(instrument.Figi, 1, OperationType.Buy, price);
                        var placedOrder = await _context.PlaceLimitOrderAsync(order);

                        tradeData.OrderId = placedOrder.OrderId;
                        tradeData.BuyPrice = price;
                        tradeData.StopPrice = Helpers.RoundPrice(tradeData.BuyPrice * (decimal)0.99, instrument.MinPriceIncrement);
                        tradeData.Status = Status.BuyPending;


                        Logger.Write("{0}: Buy. Price: {1}. StopPrice: {2}. Candle: {3}. Details: Reason: 'localChange >= 0 && historyGrow', Status - {4}, RejectReason -  {5}",
                            instrument.Ticker, tradeData.BuyPrice, tradeData.StopPrice, JsonConvert.SerializeObject(candle), placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                    else if (momentumChange >= 1 && (localChange >= 0 || momentumChange >= 2 * Math.Abs(localChange)))
                    {
                        // buy 1 lot
                        var price = candle.Close + 2 * instrument.MinPriceIncrement;
                        var order = new LimitOrder(instrument.Figi, 1, OperationType.Buy, price);
                        var placedOrder = await _context.PlaceLimitOrderAsync(order);

                        tradeData.OrderId = placedOrder.OrderId;
                        tradeData.BuyPrice = price;
                        tradeData.StopPrice = Helpers.RoundPrice(tradeData.BuyPrice * (decimal)0.99, instrument.MinPriceIncrement);
                        tradeData.Status = Status.BuyPending;

                        Logger.Write("{0}: Buy. BuyPrice: {1}. StopPrice: {2}. Candle: {3}. Details: Reason: 'momentumChange', Status - {4}, RejectReason -  {5}",
                            instrument.Ticker, tradeData.BuyPrice, tradeData.StopPrice, JsonConvert.SerializeObject(candle), placedOrder.Status.ToString(), placedOrder.RejectReason);
                    }
                }
            }
            else if (tradeData.Status == Status.BuyPending)
            {
                // check if limited order executed
                var orders = await _context.OrdersAsync();
                bool found = false;
                foreach (var order in orders)
                {
                    if (order.OrderId == tradeData.OrderId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // order executed
                    tradeData.Status = Status.BuyDone;
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
                if (candle.Close <= tradeData.StopPrice)
                {
                    var price = candle.Close - 2 * instrument.MinPriceIncrement;
                    var order = new LimitOrder(instrument.Figi, 1, OperationType.Sell, price);
                    var placedOrder = await _context.PlaceLimitOrderAsync(order);

                    Logger.Write("{0}: Sell by SL. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, price, JsonConvert.SerializeObject(candle), tradeData.StopPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.StopPrice));
                    tradeData.Status = Status.SellPending;

                    _stats.Update(tradeData.BuyPrice, price);
                }
                else if (tradeData.StopPrice < tradeData.BuyPrice && Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close) >= (decimal)0.5)
                {
                    // move stop loss to no loss
                    tradeData.StopPrice = tradeData.BuyPrice;//Helpers.RoundPrice(tradeData.BuyPrice * (decimal)1.005, instrument.MinPriceIncrement);
                    Logger.Write("{0}: Moving stop loss to no loss. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                }
                else if (tradeData.StopPrice >= tradeData.BuyPrice && Helpers.GetChangeInPercent(tradeData.StopPrice, candle.Close) >= (decimal)1.0)
                {
                    // pulling the stop to the price
                    tradeData.StopPrice = Helpers.RoundPrice(candle.Close * (decimal)0.995, instrument.MinPriceIncrement);
                    Logger.Write("{0}: Pulling stop loss to current price. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                }
            }
            else if (tradeData.Status == Status.ShutDown)
            {
                if (tradeData.BuyPrice != 0)
                {
                    _stats.Update(tradeData.BuyPrice, candle.Close);
                }

                //TODO
                //break;
            }
        }

        private void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                _lastCandleReceived = DateTime.Now;

                var cr = (CandleResponse)e.Response;

                Quotes candles;
                if (_candles.TryGetValue(cr.Payload.Figi, out candles))
                {
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
                }
                else
                {
                    var list = new Quotes(cr.Payload.Figi, _figiToTicker[cr.Payload.Figi]);
                    list.Candles.Add(cr.Payload);
                    _candles.Add(cr.Payload.Figi, list);
                }

                _candles[cr.Payload.Figi].QuoteLogger.onQuoteReceived(cr.Payload);
                OnCandleUpdate(cr.Payload.Figi);
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