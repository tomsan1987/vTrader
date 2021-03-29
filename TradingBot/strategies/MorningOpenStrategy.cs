using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class MorningOpenStrategy : IStrategy
    {
        // Trade settings
        public const decimal sMinVolume = 250; // USD
        static public int s_OrdersPerTrade = 2; // it means we can place one more order to buy the dip more

        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes, out LimitOrder order)
        {
            order = null;

            if (tradeData.Status == Status.Watching)
            {
                return OnStatusWatching(instrument, tradeData, quotes, out order);
            }
            else if (tradeData.Status == Status.BuyPending)
            {
                return OnStatusBuyPending(instrument, tradeData, quotes, out order);
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                return OnStatusBuyDone(instrument, tradeData, quotes, out order);
            }
            else if (tradeData.Status == Status.SellPending)
            {
                return OnStatusSellPending(instrument, tradeData, quotes, out order);
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "MorningOpenStrategy";
        }

        private IStrategy.StrategyResultType OnStatusWatching(MarketInstrument instrument, TradeData tradeData, Quotes quotes, out LimitOrder order)
        {
            order = null;
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            // Working only on open market
            if ((candle.Time.Hour != 7 && candle.Time.Hour != 4) || candle.Time.Minute != 0)
                return IStrategy.StrategyResultType.NoOp;

            // make sure that it will not market open at 4h: on market open we only can have previous day close candle and current candle
            if (candle.Time.Hour == 7 && candles.Count > 2)
                return IStrategy.StrategyResultType.NoOp;

            if (quotes.Raw.Count < 10)
                return IStrategy.StrategyResultType.NoOp;

            if (candle.Volume < 100)
                return IStrategy.StrategyResultType.NoOp;

            if (tradeData.PrevDayClosePrice == -1)
            {
                // calculate previous day close price
                // theoretically there next situation: current candle is market open candle and previous candle should be previous day close candle(it could be 1 or more repeated candles)
                if (candles.Count == 1)
                {
                    // we have no previous day close candle
                    tradeData.PrevDayClosePrice = 0;
                }
                else
                {
                    var prevChange = Helpers.GetChangeInPercent(candles[candles.Count - 2]);
                    if (Math.Abs(prevChange) > 1.5m && candles[candles.Count - 2].Volume < 10)
                    {
                        // we cant rely on this data due to price significantly changed with low volume
                        tradeData.PrevDayClosePrice = 0;
                    }
                    else
                    {
                        tradeData.PrevDayClosePrice = candles[candles.Count - 2].Close;
                    }
                }
            }

            bool buy = false;
            var currCandleChange = Helpers.GetChangeInPercent(candle);
            decimal changeFromPrevClose = 0.0m;
            if (tradeData.PrevDayClosePrice > 0)
            {
                // we have previous day close price, lets estimate how we open relatively this price
                changeFromPrevClose = Helpers.GetChangeInPercent(tradeData.PrevDayClosePrice, candle.Close);

                // TODO
                //if (changeFromPrevClose > 5m /*&& short-able*/)
                //    return IStrategy.StrategyResultType.Sell;

                if (changeFromPrevClose > 0.5m) // TODO currCandleChange < 4.0m then ignore
                    return IStrategy.StrategyResultType.NoOp; // opened with gap up and price does not interesting now

                if (currCandleChange < -2.0m || changeFromPrevClose < -2.0m)
                    buy = true;
            }
            else
            {
                // we have no previous day close price, so buy only big falls
                if (currCandleChange < -3.0m)
                    buy = true;
            }

            if (buy)
            {
                // place limited order
                int lots = Math.Max((int)(sMinVolume / candle.Close), 1);
                order = new LimitOrder(instrument.Figi, lots, OperationType.Buy, candle.Close);

                Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. Lots: {3}. ChangeOpenToCurrent: {4}. ChangePrevCloseToCurrent: {5}. {6}.",
                    instrument.Ticker, Description(), order.Price, order.Lots, currCandleChange, changeFromPrevClose, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                return IStrategy.StrategyResultType.Buy;
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        private IStrategy.StrategyResultType OnStatusBuyPending(MarketInstrument instrument, TradeData tradeData, Quotes quotes, out LimitOrder order)
        {
            order = null;
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.BuyTime.AddMinutes(5) <= candle.Time)
            {
                Logger.Write("{0}: Cancel order. Did not bought at market open. {1}", instrument.Ticker, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));
                return IStrategy.StrategyResultType.CancelOrder;
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        private IStrategy.StrategyResultType OnStatusBuyDone(MarketInstrument instrument, TradeData tradeData, Quotes quotes, out LimitOrder order)
        {
            order = null;
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            var profit = Helpers.GetChangeInPercent(tradeData.AvgPrice, candle.Close);
            if (tradeData.BuyTime == candle.Time && tradeData.Orders.Count < s_OrdersPerTrade)
            {
                var lastBuyPrice = tradeData.Orders[tradeData.Orders.Count - 1].Price;
                var changeFromLastBuy = Helpers.GetChangeInPercent(lastBuyPrice, candle.Close);
                if (changeFromLastBuy < -2.0m)
                {
                    // buy more
                    int lots = Math.Max((int)(sMinVolume / candle.Close), 1);
                    order = new LimitOrder(instrument.Figi, lots, OperationType.Buy, candle.Close);

                    Logger.Write("{0}: Buy more. Price: {1}. Lots: {2}. changeFromLastBuy: {3}. {4}.",
                        instrument.Ticker, order.Price, order.Lots, changeFromLastBuy, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                    return IStrategy.StrategyResultType.Buy;
                }
            }

            // if price is growing - waiting
            var candleChange = Helpers.GetChangeInPercent(candle);
            var changeFromMax = Helpers.GetChangeInPercent(candle.High, candle.Close);

            const decimal changeThreshold = 0.0m;
            // const decimal changeThreshold = -0.2m; // suspicious change, try when more test cases
            if (candleChange >= changeThreshold && changeFromMax > -1.0m)
                return IStrategy.StrategyResultType.NoOp;

            if (candle.Time >= tradeData.BuyTime.AddMinutes(5) && profit >= 0.1m)
            {
                // just sell per current price
                order = new LimitOrder(instrument.Figi, tradeData.Lots, OperationType.Sell, candle.Close);
                Logger.Write("{0}: Morning closing. Close price: {1}. Lots: {2}. CandleChange: {3}%. ChangeFromCandleHigh: {4}%. Profit: {5}({6}%). {7}",
                    instrument.Ticker, order.Price, order.Lots, candleChange, changeFromMax, order.Price - tradeData.AvgPrice, profit, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                return IStrategy.StrategyResultType.Sell;
            }

            if (candle.Time > tradeData.BuyTime.AddMinutes(15))
            {
                // we have waited 20 minutes in hope to profit, just sell per current price
                order = new LimitOrder(instrument.Figi, tradeData.Lots, OperationType.Sell, candle.Close);
                Logger.Write("{0}: Morning closing by timeout. Close price: {1}. Lots: {2}. Profit: {3}({4}%). {5}",
                    instrument.Ticker, order.Price, order.Lots, order.Price - tradeData.AvgPrice, profit, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                return IStrategy.StrategyResultType.Sell;
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        private IStrategy.StrategyResultType OnStatusSellPending(MarketInstrument instrument, TradeData tradeData, Quotes quotes, out LimitOrder order)
        {
            order = null;
            // TODO

            return IStrategy.StrategyResultType.NoOp;
        }
    }

    public class MorningOpenStrategyData : IStrategyData
    {
        public int OutsetStart { get; set; }
        public int OutsetEnd { get; set; }
        public decimal Max { get; set; }
        public decimal Min { get; set; }
        public decimal Change { get; set; }
        public Trend PrevTrend { get; set; }
        public Trend CurrTrend { get; set; }
    }
}
