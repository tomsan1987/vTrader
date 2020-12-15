using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class GoodGrowStrategy : IStrategy
    {
        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.Status == Status.Watching)
            {
                // check that we did not bought it recently
                if (tradeData.Time.AddMinutes(10) >= candle.Time)
                    return IStrategy.StrategyResultType.NoOp;

                decimal volume = 0;
                for (int i = Math.Max(0, candles.Count - 3); i < candles.Count; ++i)
                    volume += candles[i].Volume;

                if (volume < 1000)
                    return IStrategy.StrategyResultType.NoOp;

                int start = candles.Count - 4;
                if (start < 0)
                    return IStrategy.StrategyResultType.NoOp;

                // check that is not fall before
                decimal min = decimal.MaxValue;
                decimal max = decimal.MinValue;
                int timeAgoStart = Math.Max(0, start - 24); // last 2 hours
                for (int i = timeAgoStart; i < start; ++i)
                {
                    if (candles[i].Close != candles[i].Open) // ignore zero candles
                    {
                        if (candles[i].Volume > 100)
                        {
                            if (candles[i].Open < candles[i].Close)
                            {
                                min = Math.Min(min, candles[i].Open);
                                max = Math.Max(max, candles[i].Close);
                            }
                            else
                            {
                                min = Math.Min(min, candles[i].Close);
                                max = Math.Max(max, candles[i].Open);
                            }
                        }
                    }
                }

                if (candle.Close < max)
                    return IStrategy.StrategyResultType.NoOp;

                string reason;
                var changeFromMax = Helpers.GetChangeInPercent(max, candle.Close);
                if (changeFromMax > 2.0m && Helpers.GetChangeInPercent(candle) > 0m && Helpers.GetChangeInPercent(candles[candles.Count - 2].Close, candle.Open) < changeFromMax)
                {
                    reason = String.Format("DayMax: {0}. Current change: +{1}%", max, changeFromMax);
                }
                else
                {
                    reason = "Grow:";
                    int greenCandles = 0;
                    for (int i = start; i < candles.Count; ++i)
                    {
                        var change = Helpers.GetChangeInPercent(candles[i]);
                        if (change >= 0.3m && change <= 1.7m)
                            ++greenCandles;

                        if (i > 0 && candles[i - 1].Close >= candles[i].Close)
                            return IStrategy.StrategyResultType.NoOp;

                        reason += change.ToString() + "%;";
                    }

                    if (greenCandles != 4)
                        return IStrategy.StrategyResultType.NoOp;

                    // check if price grow too much and it is too late...
                    if (Helpers.GetChangeInPercent(candles[start].Open, candles[candles.Count - 1].Close) >= 7m)
                        return IStrategy.StrategyResultType.NoOp;
                }


                tradeData.BuyPrice = candle.Close;
                tradeData.StopLoss = candles[candles.Count - 3].Open;
                tradeData.MaxPrice = candle.Close;
                Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. Candle: {4}. Details: Reason: {5}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, JsonConvert.SerializeObject(candle), reason);
                return IStrategy.StrategyResultType.Buy;

                //var change = Helpers.GetChangeInPercent(candles[candles.Count - 1].Open, candle.Close);
                //if (change >= sGrowSize)
                //{
                //    // check previous candle
                //    if (candles.Count - 2 >= 0)
                //    {
                //        var changePrevCandle = Helpers.GetChangeInPercent(candles[candles.Count - 2]);
                //        if (changePrevCandle > -1.0m)
                //        {
                //            // check that it is not grow after fall
                //            int timeAgoStart = Math.Max(0, candles.Count - 24);
                //            var localChange = Helpers.GetChangeInPercent(candles[timeAgoStart].Close, candles[candles.Count - 2].Close);
                //            if ((localChange >= 0 && localChange < change) || (localChange < 0 && 4 * Math.Abs(localChange) < change))
                //            {
                //                // high of previous candle should be less than current price
                //                if (candles[candles.Count - 2].High < candle.Close)
                //                {
                //                    tradeData.BuyPrice = candle.Close + instrument.MinPriceIncrement;
                //                    reason = String.Format("grow +{0}%, local change {1}%, prev change {2}%", change, localChange, changePrevCandle);
                //                    Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. Candle: {4}. Details: Reason: {4}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, JsonConvert.SerializeObject(candle), reason);
                //                    return IStrategy.StrategyResultType.Buy;
                //                }
                //            }
                //        }
                //    }
                //}
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                //check if we reach stop conditions
                if (candle.Close < tradeData.StopLoss)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: SL reached. Pending. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, tradeData.SellPrice, JsonConvert.SerializeObject(candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else if (tradeData.TakeProfit > 0m && candle.Close >= tradeData.TakeProfit)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: TP reached. Pending. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, tradeData.SellPrice, JsonConvert.SerializeObject(candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else if (candle.Time > tradeData.Time.AddMinutes(5))
                {
                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Close);
                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Open);

                    // do not make any action if candle is red
                    if (candle.Open < candle.Close)
                    {
                        //if (tradeData.StopLoss < tradeData.BuyPrice && candle.Close > tradeData.BuyPrice)
                        //{
                        //    // move stop loss to no loss
                        //    tradeData.StopLoss = tradeData.BuyPrice;
                        //    tradeData.Time = candle.Time;
                        //    Logger.Write("{0}: Moving stop loss to no loss. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopLoss, JsonConvert.SerializeObject(candle));
                        //}
                        //else if (tradeData.StopLoss >= tradeData.BuyPrice && candle.Close >= tradeData.MaxPrice)

                        if (candles[candles.Count - 2].Open < candles[candles.Count - 2].Close && candle.Close * 1.001m >= tradeData.MaxPrice)
                        {
                            var minPrice = Math.Min(candles[candles.Count - 3].Open, candles[candles.Count - 3].Close);
                            if (tradeData.StopLoss < minPrice)
                            {
                                // pulling the stop to the price
                                tradeData.StopLoss = minPrice;
                                tradeData.Time = candle.Time;
                                Logger.Write("{0}: Pulling stop loss to current price. Price: {1}. MaxPrice: {2}. Candle: {3}.", instrument.Ticker, tradeData.StopLoss, tradeData.MaxPrice, JsonConvert.SerializeObject(candle));
                            }
                        }
                    }
                }
                else if (candle.Time > tradeData.BuyTime.AddMinutes(20) && tradeData.BuyTime == tradeData.Time)
                {
                    if (tradeData.BuyPrice >= candle.Close && tradeData.BuyPrice * 1.002m < candle.Close)
                    {
                        // we have a little profit, but the price does not grow, close the order with minimal profit
                        tradeData.StopLoss = candle.Close;
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Price does not grow. Closing with minimal profit. Price: {1}. Candle: {2}.", instrument.Ticker, tradeData.StopLoss, JsonConvert.SerializeObject(candle));
                    }
                    else if (tradeData.StopLoss < tradeData.BuyPrice && tradeData.BuyPrice > candle.Close)
                    {
                        // something went wrong, suffer losses
                        // try to set up Take Profit by previous candles
                        var start = Math.Max(0, candles.Count - 4);
                        while (start < candles.Count && candles[start].Time <= tradeData.BuyTime)
                            ++start;

                        decimal avg = 0m;
                        for (int i = start; i < candles.Count - 1; ++i)
                            avg += Math.Max(candles[i].Open, candles[i].Close);

                        avg = avg / candles.Count - start - 2;

                        if (avg > tradeData.StopLoss)
                        {
                            tradeData.TakeProfit = avg;
                            tradeData.Time = candle.Time;
                            Logger.Write("{0}: Price does not grow. Try to set TakeProfit. Price: {1}. Candle: {2}.", instrument.Ticker, tradeData.TakeProfit, JsonConvert.SerializeObject(candle));
                        }
                    }
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "GoodGrowStrategy";
        }
    }
}
