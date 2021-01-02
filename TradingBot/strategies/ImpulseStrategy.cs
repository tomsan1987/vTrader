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
        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.Status == Status.Watching)
            {
                if (candles.Count < 8)
                    return IStrategy.StrategyResultType.NoOp;

                // check that we did not bought it recently
                if (tradeData.Time.AddMinutes(10) >= candle.Time)
                    return IStrategy.StrategyResultType.NoOp;

                if (candle.Volume < 500)
                    return IStrategy.StrategyResultType.NoOp;

                var countQuotes = quotes.Raw.Count - quotes.RawPosStart[candles.Count - 1];
                if (countQuotes < 10)
                    return IStrategy.StrategyResultType.NoOp;

                var change = Helpers.GetChangeInPercent(candle);
                if (change < 0.5m || change < 3 * quotes.AvgCandleChange)
                    return IStrategy.StrategyResultType.NoOp;

                // check if it was outlet before
                int startOutset = candles.Count - 8;
                int endOutset = candles.Count - 1;

                bool ignoreHistory = true;
                for (int i = startOutset + 1; i < endOutset; ++i)
                {
                    if (candles[i].Volume > 100)
                    {
                        ignoreHistory = false;
                        break;
                    }
                }

                decimal A, B, maxDeviation;
                if (ignoreHistory && change > 1m)
                {
                    Trend candleTrend = new Trend();
                    candleTrend.StartPos = quotes.RawPosStart[candles.Count -1];
                    candleTrend.EndPos = quotes.Raw.Count;
                    Helpers.Approximate(quotes.Raw, candleTrend.StartPos, candleTrend.EndPos, step, out A, out B, out maxDeviation);
                    candleTrend.A = A;
                    candleTrend.B = B;
                    candleTrend.MaxFall= maxDeviation;

                    tradeData.Trend = candleTrend;
                    tradeData.BuyPrice = candle.Close;
                    tradeData.StopLoss = candle.Low;
                    string reason = String.Format("No history due to low volumes. CandleChange: +{0}%.", change);
                    Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. Candle: ID:{4}, Time: {5}, Close: {6}. Details: {7}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, reason);
                    return IStrategy.StrategyResultType.Buy;
                }

                // currend candle open should be at least at average of previous candle
                var prevCandleChange = Helpers.GetChangeInPercent(candles[candles.Count - 2]);
                if (prevCandleChange > 0.5m)
                {
                    var openThreshold = (candles[candles.Count - 2].Close + candles[candles.Count - 2].Open) / 2;
                    if (candle.Open < openThreshold)
                        return IStrategy.StrategyResultType.NoOp;
                }

                Trend trend = new Trend();
                trend.StartPos = quotes.RawPosStart[startOutset];
                trend.EndPos = quotes.RawPosStart[endOutset];

                Helpers.Approximate(quotes.Raw, trend.StartPos, trend.EndPos, step, out A, out B, out maxDeviation);

                if (A > 0)
                {
                    decimal min = candles[startOutset].Low;
                    decimal max = candles[startOutset].High;
                    for (int i = startOutset + 1; i < endOutset; ++i)
                    {
                        //min = Math.Min(min, Math.Min(candles[i].Close, candles[i].Open));
                        //max = Math.Max(max, Math.Max(candles[i].Close, candles[i].Open));
                        min = Math.Min(min, candles[i].Low);
                        max = Math.Max(max, candles[i].High);
                    }

                    var chageMinMax = Helpers.GetChangeInPercent(min, max);
                    if (chageMinMax < 1m && candle.Close > max && change > chageMinMax)
                    {
                        var timeFrom = candles[startOutset].Time.ToShortTimeString();
                        var timeTo = candles[endOutset].Time.ToShortTimeString();
                        string reason = String.Format("OutletTime: {0}({1})-{2}({3}). OutletMinMax: {4}/{5}. OutletAB: {6}/{7}. CurrentChange: +{8}%. DayAvgChange: {9}%", timeFrom, trend.StartPos, timeTo, trend.EndPos, min, max, A, B, change, quotes.AvgCandleChange);

                        Trend candleTrend = new Trend();
                        candleTrend.StartPos = quotes.RawPosStart[candles.Count - 1];
                        candleTrend.EndPos = quotes.Raw.Count;
                        Helpers.Approximate(quotes.Raw, candleTrend.StartPos, candleTrend.EndPos, step, out A, out B, out maxDeviation);
                        candleTrend.A = A;
                        candleTrend.B = B;
                        candleTrend.MaxFall = maxDeviation;
                        if (A > 0)
                        {
                            reason += String.Format(". MaxDeviation: {0}%", maxDeviation);

                            tradeData.Trend = candleTrend;
                            tradeData.BuyPrice = candle.Close;
                            tradeData.StopLoss = candle.Low;
                            Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. Candle: ID:{4}, Time: {5}, Close: {6}. Details: {7}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, reason);
                            return IStrategy.StrategyResultType.Buy;
                        }
                    }
                }


                //// after outlet there should be: 1 green candle and the next(current candle should be higher than previous)
                //if (tradeData.StrategyData != null)
                //{
                //    var strategyData = tradeData.StrategyData as ImpulseStrategyData;
                //    if (strategyData.OutsetEnd + 2 == candles.Count)
                //    {
                //        var change = Helpers.GetChangeInPercent(strategyData.Max, candle.Close);
                //        if (change > strategyData.Change)
                //        {
                //            if (strategyData.OutsetEnd < candles.Count && candles[strategyData.OutsetEnd].Open < candles[strategyData.OutsetEnd].Close)
                //            {
                //                // confirmed hole the max of outlet
                //                var timeFrom = candles[strategyData.OutsetStart].Time.ToShortTimeString();
                //                var timeTo = candles[strategyData.OutsetEnd].Time.ToShortTimeString();
                //                string reason = String.Format("OutletTime: {0}-{1}. OutletMax: {2}. OutletChange: {3}. CurrentChange: +{4}%. Day avg change: {2}%", timeFrom, timeTo, strategyData.Max, strategyData.Change, change);

                //                tradeData.BuyPrice = candle.Close;
                //                tradeData.StopLoss = candles[strategyData.OutsetEnd].Open;
                //                Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopLoss: {3}. Candle: {4}. Details: {5}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopLoss, JsonConvert.SerializeObject(candle), reason);
                //                return IStrategy.StrategyResultType.Buy;
                //            }
                //        }
                //        else
                //        {
                //            return IStrategy.StrategyResultType.NoOp;
                //        }
                //    }
                //    else
                //    {
                //        tradeData.StrategyData = null;
                //    }
                //}

                //if (tradeData.BuyTime >= candle.Time)
                //    return IStrategy.StrategyResultType.NoOp;

                //tradeData.BuyTime = candle.Time;

                //// build trend
                //Trend trend = new Trend();

                //int startOutset = candles.Count - 8;
                //int endOutset = candles.Count - 2;

                //trend.StartPos = quotes.RawPosStart[startOutset];
                //trend.EndPos = quotes.RawPosStart[endOutset];
                //decimal step = (decimal)((candles[endOutset].Time - candles[startOutset].Time).TotalSeconds / (trend.EndPos - trend.StartPos));

                //decimal A, B;
                //Helpers.Approximate(quotes.Raw, trend.StartPos, trend.EndPos, step, out A, out B);

                //// check if it was `outset`
                //decimal min = candles[startOutset].Low;
                //decimal max = candles[startOutset].High;
                //decimal volume = candles[startOutset].Volume;
                //decimal avgChange = Math.Abs(Helpers.GetChangeInPercent(candles[startOutset]));
                ////decimal M = candles[startOutset].Low + candles[startOutset].Open + candles[startOutset].Close + candles[startOutset].High;
                //decimal M = candles[startOutset].Close;
                //for (int i = startOutset + 1; i < endOutset; ++i)
                //{
                //    min = Math.Min(min, Math.Min(candles[i].Close, candles[i].Open));
                //    max = Math.Max(max, Math.Max(candles[i].Close, candles[i].Open));
                //    volume += candles[i].Volume;
                //    avgChange += Math.Abs(Helpers.GetChangeInPercent(candles[i]));
                //    //M += candles[i].Low + candles[i].Open + candles[i].Close + candles[i].High;
                //    M += candles[i].Close;
                //}

                //if (volume < 200)
                //    return IStrategy.StrategyResultType.NoOp;

                //M = M / ((endOutset - startOutset) /** 4*/);

                //decimal S = 0;
                //for (int i = startOutset; i < endOutset; ++i)
                //{
                //    //S += (candles[i].Low - M) * (candles[i].Low - M);
                //    //S += (candles[i].Open - M) * (candles[i].Open - M);
                //    S += (candles[i].Close - M) * (candles[i].Close - M);
                //    //S += (candles[i].High - M) * (candles[i].High- M);
                //}

                //S /= (endOutset - startOutset) /** 4*/;
                //S = Math.Round((decimal)Math.Sqrt((double)S), 3);
                //M = Math.Round(M, 2);

                //avgChange /= (endOutset - startOutset);
                //var outsetChange = Math.Abs(Helpers.GetChangeInPercent(min, max));
                //if (outsetChange > 0.5m || avgChange * 3 > outsetChange)
                //    return IStrategy.StrategyResultType.NoOp; // does not look like outset

                //tradeData.StrategyData = new ImpulseStrategyData();
                //var data = tradeData.StrategyData as ImpulseStrategyData;
                //data.OutsetStart = startOutset;
                //data.OutsetEnd = endOutset;
                //data.Change = outsetChange;
                //data.Min = min;
                //data.Max = max;

                return IStrategy.StrategyResultType.NoOp;
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                //check if we reach stop conditions
                if (candle.Close < tradeData.StopLoss)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: SL reached. Pending. Close price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}. Profit: {5}({6}%)", instrument.Ticker, tradeData.SellPrice, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else if (tradeData.TakeProfit > 0m && candle.Close >= tradeData.TakeProfit)
                {
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: TP reached. Pending. Close price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}. Profit: {5}({6}%)", instrument.Ticker, tradeData.SellPrice, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close, tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else
                {
                    // check if price is grow for 0.5% from buy price - set no loss
                    var change = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                    if (tradeData.BuyTime == candle.Time && (change > 0.5m || change > tradeData.Trend.MaxFall) && tradeData.StopLoss < tradeData.BuyPrice)
                    {
                        // pulling the stop to no loss
                        tradeData.StopLoss = tradeData.BuyPrice;
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Pulling stop loss to no loss. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                        return IStrategy.StrategyResultType.NoOp;
                    }

                    // if price frow from SL more than 2% - pull SL to price for 1%
                    change = Helpers.GetChangeInPercent(tradeData.StopLoss, candle.Close);
                    if (tradeData.BuyTime == candle.Time && change > 2m && tradeData.StopLoss >= tradeData.BuyPrice)
                    {
                        // pulling the stop to price
                        tradeData.StopLoss = Helpers.RoundPrice(candle.Close * 0.99m, instrument.MinPriceIncrement);
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Pulling stop loss to current price. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                        return IStrategy.StrategyResultType.NoOp;
                    }

                    if (tradeData.BuyTime == candle.Time)
                    {
                        // check that price does not much different from the trend
                        var trendPrice = tradeData.Trend.A * (quotes.Raw.Count - tradeData.Trend.StartPos) * step + tradeData.Trend.B;
                        var changeFromTrend = Helpers.GetChangeInPercent(trendPrice, candle.Close);
                        if (changeFromTrend < 0 && Math.Abs(changeFromTrend) > tradeData.Trend.MaxFall)
                        {
                            var currentProfit = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                            if (currentProfit > 0)
                            {
                                // due to price diff from the trend - move SL to some profit zone
                                var newSL = Helpers.RoundPrice((tradeData.BuyPrice + candle.Close) / 2, instrument.MinPriceIncrement);
                                if (newSL > tradeData.StopLoss)
                                {
                                    // pulling the stop to price
                                    tradeData.StopLoss = newSL;
                                    tradeData.Time = candle.Time;
                                    Logger.Write("{0}: Price is diff from trend. Move SL to some profit zone. Price: {1}. ChangeFromTrend: {2}. Candle: ID:{3}, Time: {4}, Close: {5}.", instrument.Ticker, tradeData.StopLoss, changeFromTrend, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                                    return IStrategy.StrategyResultType.NoOp;
                                }
                            }
                        }
                    }

                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Close);
                    tradeData.MaxPrice = Math.Max(tradeData.MaxPrice, candles[candles.Count - 2].Open);

                    // do not make any action if candle is red
                    bool SLset = false;
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
                                Logger.Write("{0}: Pulling stop loss to current price. Price: {1}. MaxPrice: {2}. Candle: ID:{3}, Time: {4}, Close: {5}.", instrument.Ticker, tradeData.StopLoss, tradeData.MaxPrice, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                                SLset = true;
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
                            Logger.Write("{0}: Price does not grow. Closing with minimal profit. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.StopLoss, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                        }
                        else if (tradeData.StopLoss < tradeData.BuyPrice && tradeData.BuyPrice > candle.Close)
                        {
                            // something went wrong, suffer losses
                            // try to set up Take Profit by previous candles
                            var start = Math.Max(0, candles.Count - 4);
                            while (start < candles.Count && candles[start].Time <= tradeData.BuyTime)
                                ++start;

                            decimal avg = 0m;
                            for (int i = start; i < candles.Count; ++i)
                                avg += Math.Max(candles[i].Open, candles[i].Close);

                            avg = Helpers.RoundPrice(avg / (candles.Count - start), instrument.MinPriceIncrement);

                            if (avg > tradeData.StopLoss)
                            {
                                tradeData.TakeProfit = avg;
                                tradeData.Time = candle.Time;
                                Logger.Write("{0}: Price does not grow. Try to set TakeProfit. Price: {1}. Candle: ID:{2}, Time: {3}, Close: {4}.", instrument.Ticker, tradeData.TakeProfit, quotes.Raw.Count, candle.Time.ToShortTimeString(), candle.Close);
                            }
                        }
                    }
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "ImpulseStrategy";
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
