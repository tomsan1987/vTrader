using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class ImpulseStrategy : IStrategy
    {
        static public decimal step = 1m / 1000;
        static public int s_min_quotes_per_candle = 40;
        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.Status == Status.Watching)
            {
                // do not buy on market open
                if (candle.Time.Hour == 7 && candle.Time.Minute == 0)
                    return IStrategy.StrategyResultType.NoOp;

                // do not buy on post market
                if (candle.Time.Hour >= 21)
                    return IStrategy.StrategyResultType.NoOp;

                // do not trade on spikes
                if (quotes.SpikePositions.Count > 0 && quotes.SpikePositions[quotes.SpikePositions.Count - 1] + s_min_quotes_per_candle > quotes.Raw.Count)
                    return IStrategy.StrategyResultType.NoOp;

                //// check for zero candles and high volatile on premarket
                //// doesnt work: in premarket it could be high volatile and it does not mean mothing
                //bool badPremarket = false;
                //if (candle.Time.Hour == 14 && candle.Time.Minute == 30)
                //{
                //    int zeroCandles = 0;
                //    decimal min = decimal.MaxValue;
                //    decimal max = decimal.MinValue;
                //    for (int i = 0; i < candles.Count; ++i)
                //    {
                //        min = Math.Min(min, candles[i].Low);
                //        max = Math.Max(max, candles[i].High);
                //        if (candles[i].Volume < 5 || (candles[i].Open == candles[i].Close && candles[i].Low == candles[i].High))
                //        {
                //            ++zeroCandles;
                //        }
                //    }

                //    if (zeroCandles > 5 && Math.Abs(Helpers.GetChangeInPercent(min, max)) > 3m)
                //        badPremarket = true;
                //        //return IStrategy.StrategyResultType.NoOp; // bad premarket, ignore this candle
                //}

                var fallChange = Helpers.GetChangeInPercent(quotes.DayMax, candle.Close);
                if (fallChange < -4m)
                    return IStrategy.StrategyResultType.NoOp;

                // found change for last hour
                int startOutset = Math.Max(0, candles.Count - 8);
                int endOutset = candles.Count - 1;

                decimal min = candles[startOutset].Low;
                decimal max = candles[startOutset].High;
                for (int i = startOutset + 1; i < endOutset; ++i)
                {
                    min = Math.Min(min, candles[i].Low);
                    max = Math.Max(max, candles[i].High);
                }

                var chageMinMax = Helpers.GetChangeInPercent(min, max);
                //if (chageMinMax > 3m)
                //    return IStrategy.StrategyResultType.NoOp;

                // check that we did not bought it recently
                if (tradeData.Time.AddMinutes(10) >= candle.Time)
                    return IStrategy.StrategyResultType.NoOp;

                if (candle.Volume < 500)
                    return IStrategy.StrategyResultType.NoOp;

                var countQuotes = quotes.Raw.Count - quotes.RawPosStart[candles.Count - 1];
                if (countQuotes < s_min_quotes_per_candle)
                    return IStrategy.StrategyResultType.NoOp;

                // do nothing if price is already increased more than 10% for a day
                if (candles.Count > 3 && Helpers.GetChangeInPercent(candles[1].Open, candle.Close) >= 10m)
                    return IStrategy.StrategyResultType.NoOp;

                var change = Helpers.GetChangeInPercent(candle);
                if (change < 0.5m || change < 2 * quotes.AvgCandleChange)
                    return TrendFallback(instrument, tradeData, quotes);

                // currend candle open should be at least at average of previous candle
                if (candles.Count > 1)
                {
                    var prevCandleChange = Helpers.GetChangeInPercent(candles[candles.Count - 2]);
                    if (prevCandleChange > 0.5m)
                    {
                        var openThreshold = (candles[candles.Count - 2].Close + candles[candles.Count - 2].Open) / 2;
                        if (candle.Open < openThreshold)
                            return IStrategy.StrategyResultType.NoOp;
                    }
                }

                // check if it was outlet before
                Trend trend = new Trend();
                trend.StartPos = quotes.RawPosStart[candles.Count - 1];
                trend.EndPos = quotes.Raw.Count;

                string reason = "";
                if (change > 1.0m && change > chageMinMax && candle.Close > max)
                {
                    // if change is not significantly greater than min max need to check some previous candles: it should not have big tails
                    bool prevCandlesTestPassed = true;
                    if (candles.Count > 3 && change - chageMinMax < 0.4m)
                    {
                        var prevCandle = candles[candles.Count - 2];
                        var prevCandleChange = Helpers.GetChangeInPercent(prevCandle.Low, prevCandle.High);
                        if (prevCandleChange > chageMinMax / 2)
                            prevCandlesTestPassed = false;

                        if (prevCandlesTestPassed)
                        {
                            prevCandle = candles[candles.Count - 3];
                            prevCandleChange = Helpers.GetChangeInPercent(prevCandle.Low, prevCandle.High);
                            if (prevCandleChange > chageMinMax / 2)
                                prevCandlesTestPassed = false;
                        }
                    }

                    if (prevCandlesTestPassed)
                    {
                        decimal A, B, maxPrice, maxFall, sd;
                        Helpers.Approximate(quotes.Raw, trend.StartPos, trend.EndPos, step, out A, out B, out maxPrice, out maxFall, out sd);
                        trend.A = A;
                        trend.B = B;
                        trend.Max = maxPrice;
                        trend.MaxFall = maxFall;
                        trend.SD = sd;

                        if (A > 0 && change > 2 * maxFall && maxFall < 1.5m)
                        {
                            var timeFrom = candles[startOutset].Time.ToShortTimeString();
                            var timeTo = candles[endOutset].Time.ToShortTimeString();
                            reason = String.Format("OutletTime: {0}({1})-{2}({3}). OutletMinMax: {4}/{5}. TrendAB: {6}/{7}. CurrentChange: +{8}%. DayAvgChange: {9}%",
                                timeFrom, trend.StartPos, timeTo, trend.EndPos, min, max, A, B, change, quotes.AvgCandleChange);

                            reason += String.Format(". MaxFall: {0}%, SD: {1}", maxFall, sd);
                        }
                    }
                }

                if (reason.Length > 0)
                {
                    tradeData.Trend = trend;
                    tradeData.BuyPrice = candle.Close;
                    tradeData.StopLoss = candle.Low;
                    Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. {4}. Details: {5}",
                        instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), reason);

                    return IStrategy.StrategyResultType.Buy;
                }

                return TrendFallback(instrument, tradeData, quotes);
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                // close on market closing
                if (candle.Time.Hour >= 20 && candle.Time.Minute >= 55 || candle.Time.Hour >= 21)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: Market closing. Close price: {1}. {2}. Profit: {3}({4}%)",
                        instrument.Ticker, tradeData.SellPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }

                //check if we reach stop conditions
                if (candle.Close < tradeData.StopLoss)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: SL reached. Pending. Close price: {1}. {2}. Profit: {3}({4}%)",
                        instrument.Ticker, tradeData.SellPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else if (tradeData.TakeProfit > 0m && candle.Close >= tradeData.TakeProfit)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: TP reached. Pending. Close price: {1}. {2}. Profit: {3}({4}%)",
                        instrument.Ticker, tradeData.SellPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else
                {
                    // check if price is grow for 2% from buy price - set no loss
                    var change = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                    // does not affect tests
                    if (tradeData.BuyTime.AddMinutes(5) >= candle.Time && change > 3m && tradeData.StopLoss < tradeData.BuyPrice)
                    {
                        // pulling the stop to no loss
                        tradeData.StopLoss = tradeData.BuyPrice;
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Pulling stop loss to no loss. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                        return IStrategy.StrategyResultType.NoOp;
                    }

                    // if price grow from SL more than 4% - pull SL to price for 2%
                    change = Helpers.GetChangeInPercent(tradeData.StopLoss, candle.Close);
                    // does not affect tests
                    //if (tradeData.BuyTime == candle.Time && change > 4m && tradeData.StopLoss >= tradeData.BuyPrice)
                    //{
                    //    // pulling the stop to price
                    //    tradeData.StopLoss = Helpers.RoundPrice(candle.Close * 0.98m, instrument.MinPriceIncrement);
                    //    tradeData.Time = candle.Time;
                    //    Logger.Write("{0}: Pulling stop loss to current price. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                    //    return IStrategy.StrategyResultType.NoOp;
                    //}

                    if (tradeData.BuyTime == candle.Time)
                    {
                        var trendPrice = tradeData.Trend.A * (quotes.Raw.Count - tradeData.Trend.StartPos) * step + tradeData.Trend.B;
                        var changeFromTrend = Helpers.GetChangeInPercent(trendPrice, candle.Close);
                        var changeFromMax = Helpers.GetChangeInPercent(tradeData.Trend.Max, candle.Close);
                        var changeFromBuy = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                        var maxFallLocal = Math.Max(tradeData.Trend.MaxFall, 0.5m);

                        // check that the price does not less than max fall of the trend
                        if (changeFromBuy < 0 && Math.Abs(changeFromBuy) > 2 * maxFallLocal)
                        {
                            tradeData.SellPrice = candle.Close;
                            Logger.Write("{0}: Price much differ between Buy and Max trend fall. MaxFall: {1}%. BuyPrice: {2}. MaxPrice: {3}. ChangeFromBuy: {4}%. Pending. Close price: {5}. {6}. Profit: {7}({8}%)",
                                instrument.Ticker, tradeData.Trend.MaxFall, tradeData.BuyPrice, tradeData.Trend.Max, changeFromBuy, tradeData.SellPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                            return IStrategy.StrategyResultType.Sell;
                        }

                        // check that the price does not less than max fall of the trend
                        if (changeFromMax < 0 && Math.Abs(changeFromMax) > 2 * maxFallLocal)
                        {
                            var profitLocal = Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice);
                            if (profitLocal > 0.7m || profitLocal <= 0.2m)
                            {
                                tradeData.SellPrice = candle.Close;
                                Logger.Write("{0}: Price is falling. Closing. MaxFall: {1}%, Max: {2}, ChangeFromMax: {3}%. Pending. Close price: {4}. {5}. Profit: {6}({7}%)",
                                    instrument.Ticker, tradeData.Trend.MaxFall, tradeData.Trend.Max, changeFromMax, tradeData.SellPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, profitLocal);
                                return IStrategy.StrategyResultType.Sell;
                            }
                        }

                        // check that price does not much different from the trend
                        if (changeFromTrend > tradeData.Trend.MaxFall && tradeData.Trend.EndPos + 30 < quotes.Raw.Count) // rebuild trend once on 30 quotes if need
                        {
                            // rebuild trend
                            decimal A, B, maxPrice, maxFall, sd;
                            tradeData.Trend.EndPos = quotes.Raw.Count;
                            Helpers.Approximate(quotes.Raw, tradeData.Trend.StartPos, tradeData.Trend.EndPos, step, out A, out B, out maxPrice, out maxFall, out sd);

                            Logger.Write("{0}: Rebuild trend due to changeFromTrend({1}%) > MaxFall({2}%). {3}. Trend: A: {4}, B: {5}, Max: {6}, MaxFall: {7}, SD: {8}",
                                instrument.Ticker, changeFromTrend, tradeData.Trend.MaxFall, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), A, B, maxPrice, maxFall, sd);

                            tradeData.Trend.A = A;
                            tradeData.Trend.B = B;
                            tradeData.Trend.Max = maxPrice;
                            tradeData.Trend.MaxFall = maxFall;
                            tradeData.Trend.SD = sd;

                            return IStrategy.StrategyResultType.NoOp;
                        }

                        //if (changeFromTrend < 0 && Math.Abs(changeFromTrend) > tradeData.Trend.MaxFall)
                        //{
                        //    var currentProfit = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                        //    if (currentProfit > 0)
                        //    {
                        //        // due to price diff from the trend - move SL to some profit zone
                        //        var newSL = Helpers.RoundPrice((tradeData.BuyPrice + candle.Close) / 2, instrument.MinPriceIncrement);
                        //        if (newSL > tradeData.StopLoss)
                        //        {
                        //            // pulling the stop to price
                        //            tradeData.StopLoss = newSL;
                        //            tradeData.Time = candle.Time;
                        //            Logger.Write("{0}: Price is diff from trend. Move SL to some profit zone. Price: {1}. ChangeFromTrend: {2}. Candle: ID:{3}, Time: {4}, Close: {5}.", instrument.Ticker, tradeData.StopLoss, changeFromTrend, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                        //            return IStrategy.StrategyResultType.NoOp;
                        //        }
                        //    }
                        //}

                        //// check that price does not much different from the max

                        //if (changeFromMax < 0 && Math.Abs(changeFromMax) > tradeData.Trend.MaxFall)
                        //{
                        //    tradeData.SellPrice = candle.Close;
                        //    Logger.Write("{0}: Price much differ between from maximum. MaxFall: {1}%, Max: {2}, ChangeFromMax: {3}%. Pending. Close price: {1}. Candle: ID:{4}, Time: {5}, Close: {6}. Profit: {7}({8}%)", instrument.Ticker, tradeData.Trend.MaxFall, tradeData.Trend.Max, changeFromMax, tradeData.SellPrice, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                        //    return IStrategy.StrategyResultType.Sell;
                        //}
                    }

                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Close);
                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Open);

                    var profit = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);

                    // do not make any action if candle is red
                    bool SLset = false;
                    if (candle.Open < candle.Close)
                    {
                        if (candles[candles.Count - 2].Open < candles[candles.Count - 2].Close && candle.Close >= tradeData.MaxPrice * 1.001m)
                        {
                            // do not pull SL if previous candle has not significant body
                            if (Helpers.GetChangeInPercent(candles[candles.Count - 2]) > 0.12m)
                            {
                                var minPrice = Math.Min(candles[candles.Count - 3].Open, candles[candles.Count - 3].Close);
                                if (tradeData.StopLoss < minPrice)
                                {
                                    // pulling the stop to the price
                                    tradeData.StopLoss = minPrice;
                                    tradeData.Time = candle.Time;
                                    Logger.Write("{0}: Pulling stop loss to current price. Price: {1}. MaxPrice: {2}. {3}.",
                                        instrument.Ticker, tradeData.StopLoss, tradeData.MaxPrice, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));
                                    SLset = true;
                                }
                            }
                        }
                    }

                    if (!SLset && candle.Time > tradeData.BuyTime.AddMinutes(15) && (tradeData.BuyTime == tradeData.Time || tradeData.TakeProfit > 0m))
                    {
                        if (tradeData.BuyPrice >= candle.Close && tradeData.BuyPrice * 1.002m < candle.Close)
                        {
                            // we have a little profit, but the price does not grow, close the order with minimal profit
                            tradeData.StopLoss = candle.Close;
                            tradeData.Time = candle.Time;
                            Logger.Write("{0}: Price does not grow. Closing with minimal profit. Price: {1}. {2}.",
                                instrument.Ticker, tradeData.StopLoss, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));
                        }
                        else if (tradeData.StopLoss < tradeData.BuyPrice && tradeData.BuyPrice > candle.Close)
                        {
                            // something went wrong, suffer losses
                            // try to set up Take Profit by previous candles
                            var start = Math.Max(0, candles.Count - 4);
                            while (start < candles.Count && candles[start].Time <= tradeData.BuyTime)
                                ++start;

                            decimal low = candles[start].Low;
                            decimal avg = 0m;
                            for (int i = start; i < candles.Count; ++i)
                            {
                                avg += Math.Max(candles[i].Open, candles[i].Close);
                                low = Math.Min(low, candles[i].Low);
                            }

                            avg = Helpers.RoundPrice(avg / (candles.Count - start), instrument.MinPriceIncrement);

                            if (avg > tradeData.StopLoss && avg != tradeData.TakeProfit)
                            {
                                tradeData.TakeProfit = avg;
                                tradeData.Time = candle.Time;
                                Logger.Write("{0}: Price does not grow. Try to set TakeProfit. Price: {1}. {2}.",
                                    instrument.Ticker, tradeData.TakeProfit, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));
                            }

                            if (tradeData.StopLoss < low && low < candle.Close)
                            {
                                tradeData.StopLoss = low;
                                tradeData.Time = candle.Time;
                                Logger.Write("{0}: Price does not grow. Pulling Stop Loss closer. Price: {1}. {2}.",
                                    instrument.Ticker, tradeData.StopLoss, Helpers.CandleDesc(quotes.Raw.Count - 1, candle));
                            }
                        }
                    }

                    // if price too much changed from maxPrice and we have some profit - close order
                    var max = tradeData.MaxPrice;
                    change = Helpers.GetChangeInPercent(max, candle.Close);
                    if (change < -2.0m && profit > 2.0m && tradeData.StopLoss < candle.Close)
                    {
                        tradeData.SellPrice = candle.Close;
                        Logger.Write("{0}: Price too fall from max. Max: {1}. Threshold: {2}. Price change: {3}; Closing. {4}. Profit: {5}({6}%)",
                            instrument.Ticker, max, 2.0m, change, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                        return IStrategy.StrategyResultType.Sell;
                    }

                    //var prevCandleChange = 
                    //change = Helpers.GetChangeInPercent(tradeData.MaxPrice, candle.Close);
                    //if (change < -2 * quotes.AvgCandleChange && profit < -0.2m && tradeData.StopLoss < candle.Close)
                    //{
                    //    tradeData.SellPrice = candle.Close;
                    //    Logger.Write("{0}: Price too fall from max. Closing to no loss. Max: {1}. AvgCandleChange: {2}. Price change: {3}; Closing. Candle: ID:{4}, Time: {5}, Close: {6}. Profit: {7}({8}%)",
                    //        instrument.Ticker, tradeData.MaxPrice, quotes.AvgCandleChange, change, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                    //    return IStrategy.StrategyResultType.Sell;
                    //}
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "ImpulseStrategy";
        }

        private IStrategy.StrategyResultType TrendFallback(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            const int sMinCandles = 2;
            const int sMaxCandles = 4;

            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            for (var candlesToAnalyze = sMaxCandles; candlesToAnalyze >= sMinCandles; candlesToAnalyze--)
            {
                if (candles.Count < candlesToAnalyze)
                    continue;

                // market open candles could have long tails, ignore it
                if (candles[candles.Count - candlesToAnalyze].Time.Hour == 7 && candles[candles.Count - candlesToAnalyze].Time.Minute == 0)
                    continue;

                //// check that each candle has enough quotes
                //bool quotesOK = true;
                //var offset = quotes.Raw.Count;
                //for (var i = 0; i < candlesToAnalyze; ++i)
                //{
                //    var countQuotes = offset - quotes.RawPosStart[candles.Count - i - 1];
                //    if (countQuotes < s_min_quotes_per_candle)
                //    {
                //        quotesOK = false;
                //        break;
                //    }
                //    offset -= countQuotes;
                //}

                //if (!quotesOK)
                //    continue;

                var countQuotes = quotes.Raw.Count - quotes.RawPosStart[candles.Count - candlesToAnalyze];
                if (countQuotes < 2 * s_min_quotes_per_candle)
                    continue;

                // check how much price changed last candles
                var change = Helpers.GetChangeInPercent(candles[candles.Count - candlesToAnalyze].Low, candle.Close);
                if (change < 2.0m)
                    continue;

                int startOutset = Math.Max(0, candles.Count - 6 - candlesToAnalyze);
                int endOutset = candles.Count - candlesToAnalyze;
                decimal A, B, maxPrice, maxFall, sd;

                Trend trend = new Trend();
                trend.StartPos = quotes.RawPosStart[candles.Count - candlesToAnalyze];
                trend.EndPos = quotes.Raw.Count;

                // correct start position if need
                if (countQuotes > 500 && Helpers.GetChangeInPercent(candles[candles.Count - candlesToAnalyze].Close, candles[candles.Count - candlesToAnalyze].Low) < -0.7m)
                {
                    int minPos = trend.StartPos;
                    for (int i = minPos; i < trend.EndPos; ++i)
                    {
                        if (quotes.Raw[i].Price <= quotes.Raw[minPos].Price)
                            minPos = i;

                        if (quotes.Raw[minPos].Price <= candles[candles.Count - candlesToAnalyze].Low)
                            break;
                    }

                    trend.StartPos = minPos;
                }

                Helpers.Approximate(quotes.Raw, trend.StartPos, trend.EndPos, step, out A, out B, out maxPrice, out maxFall, out sd);
                trend.A = A;
                trend.B = B;
                trend.Max = maxPrice;
                trend.MaxFall = maxFall;
                trend.SD = sd;

                if (maxFall > 1.25m)
                    continue; // too riskly

                // check how good this trend
                if (change < maxFall * 2)
                    continue;

                if (A > 0)
                {
                    decimal min = candles[startOutset].Low;
                    decimal max = candles[startOutset].High;
                    for (int i = startOutset + 1; i < endOutset; ++i)
                    {
                        min = Math.Min(min, candles[i].Low);
                        max = Math.Max(max, candles[i].High);
                    }

                    //var chageMinMax = Helpers.GetChangeInPercent(min, max);
                    if (/*chageMinMax < 1m && */candle.Close > max /*&& change > chageMinMax*/)
                    {
                        var timeFrom = candles[candles.Count - candlesToAnalyze].Time.ToShortTimeString();
                        var timeTo = candles[candles.Count - 1].Time.ToShortTimeString();
                        string reason = String.Format("Trend: {0}({1})-{2}({3}). PrevMinMax: {4}/{5}. Trend AB: {6}/{7}. Trend Max/Fall/SD: {8}/{9}/{10}; TrendGrow: +{11}%. DayAvgChange: {12}%",
                            timeFrom, trend.StartPos, timeTo, trend.EndPos, min, max, A, B, maxPrice, maxFall, sd, change, quotes.AvgCandleChange);

                        tradeData.Trend = trend;
                        tradeData.BuyPrice = candle.Close;
                        tradeData.StopLoss = Helpers.RoundPrice(candle.Close - candle.Close * (2m * Math.Max(0.5m, maxFall) / 100), instrument.MinPriceIncrement);
                        Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. {4}. Details: {5}",
                            instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, Helpers.CandleDesc(quotes.Raw.Count - 1, candle), reason);

                        return IStrategy.StrategyResultType.Buy;
                    }
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }
    }

    public class ImpulseStrategyData : IStrategyData
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
