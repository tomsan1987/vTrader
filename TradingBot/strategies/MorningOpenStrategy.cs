using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class MorningOpenStrategy : IStrategy
    {
        static public decimal step = 1m / 1000;
        static public int s_min_quotes_per_candle = 40;
        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            if (tradeData.Status == Status.Watching)
            {
                return OnStatusWatching(instrument, tradeData, quotes);
            }
            else if (tradeData.Status == Status.BuyPending)
            {
                return OnStatusBuyPending(instrument, tradeData, quotes);
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                return OnStatusBuyDone(instrument, tradeData, quotes);
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "MorningOpenStrategy";
        }

        private IStrategy.StrategyResultType OnStatusWatching(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            // Working only on open market
            if ((candle.Time.Hour != 7 && candle.Time.Hour != 4) || candle.Time.Minute != 0)
                return IStrategy.StrategyResultType.NoOp;

            if (quotes.Raw.Count < 10)
                return IStrategy.StrategyResultType.NoOp;

            if (candle.Volume < 100)
                return IStrategy.StrategyResultType.NoOp;

            if (tradeData.PrevDayClosePrice == -1)
            {
                // calculate previos day close price
                // teoretically there next situation: current candle is market open candle and previous candle should be previous day close candle(it could be 1 or more repeated candles)
                if (candles.Count == 1)
                {
                    // we have no previous day close candle
                    tradeData.PrevDayClosePrice = 0;
                }
                else
                {
                    tradeData.PrevDayClosePrice = candles[candles.Count - 2].Close;
                }
            }

            bool buy = false;
            var currCandleChange = Helpers.GetChangeInPercent(candle);
            decimal changeFromPrevClose = 0.0m;
            if (tradeData.PrevDayClosePrice > 0)
            {
                // we have previous day close price, lets estimate how we open relatevitely this price
                changeFromPrevClose = Helpers.GetChangeInPercent(tradeData.PrevDayClosePrice, candle.Close);

                // TODO
                //if (changeFromPrevClose > 5m /*&& shortable*/)
                //    return IStrategy.StrategyResultType.Sell;

                if (changeFromPrevClose > 0.5m)
                    return IStrategy.StrategyResultType.NoOp; // opened with gap up and price does not intresting now

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
                tradeData.BuyPrice = candle.Close;

                Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. ChangeOpenToCurrent: {3}. ChangePrevCloseToCurrent: {4}. {5}.",
                    instrument.Ticker, Description(), tradeData.BuyPrice, currCandleChange, changeFromPrevClose, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                return IStrategy.StrategyResultType.Buy;
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        private IStrategy.StrategyResultType OnStatusBuyPending(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (candle.Time.Hour >= 7 && candle.Time.Minute >= 5)
                return IStrategy.StrategyResultType.CancelOrder;

            return IStrategy.StrategyResultType.NoOp;
        }

        private IStrategy.StrategyResultType OnStatusBuyDone(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (candle.Time.Hour >= 7 && candle.Time.Minute >= 5)
            {
                // just sell per current price
                tradeData.SellPrice = candle.Close;
                Logger.Write("{0}: Morning closing. Close price: {1}. Profit: {2}({3}%). {4}",
                    instrument.Ticker, tradeData.SellPrice, tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice), Helpers.CandleDesc(quotes.Raw.Count - 1, candle));

                return IStrategy.StrategyResultType.Sell;
            }

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
