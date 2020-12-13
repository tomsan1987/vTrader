using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class RocketStrategy : IStrategy
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

                const decimal sGrowSize = 3m;

                string reason = "";

                var change = Helpers.GetChangeInPercent(candles[candles.Count - 1].Open, candle.Close);
                if (change >= sGrowSize)
                {
                    // check previous candle
                    if (candles.Count - 2 >= 0)
                    {
                        var changePrevCandle = Helpers.GetChangeInPercent(candles[candles.Count - 2]);
                        if (changePrevCandle > -1.0m)
                        {
                            // check that it is not grow after fall
                            int timeAgoStart = Math.Max(0, candles.Count - 24);
                            var localChange = Helpers.GetChangeInPercent(candles[timeAgoStart].Close, candles[candles.Count - 2].Close);
                            if ((localChange >= 0 && localChange < change) || (localChange < 0 && 4 * Math.Abs(localChange) < change))
                            {
                                // high of previous candle should be less than current price
                                if (candles[candles.Count - 2].High < candle.Close)
                                {
                                    tradeData.BuyPrice = candle.Close + instrument.MinPriceIncrement;
                                    reason = String.Format("grow +{0}%, local change {1}%, prev change {2}%", change, localChange, changePrevCandle);
                                    Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. StopPrice: {3}. Candle: {4}. Details: Reason: {4}", instrument.Ticker, Description(), tradeData.BuyPrice, tradeData.StopPrice, JsonConvert.SerializeObject(candle), reason);
                                    return IStrategy.StrategyResultType.Buy;
                                }
                            }
                        }
                    }
                }
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                //check if we reach stop conditions
                if (candle.Close < tradeData.StopPrice)
                {
                    tradeData.SellPrice = candle.Close - instrument.MinPriceIncrement;
                    Logger.Write("{0}: SL reached. Pending. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, tradeData.SellPrice, JsonConvert.SerializeObject(candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));

                    return IStrategy.StrategyResultType.Sell;
                }
                else if (tradeData.StopPrice == 0 && candle.Time > tradeData.Time.AddMinutes(5))
                {
                    var change = Helpers.GetChangeInPercent(tradeData.BuyPrice, candle.Close);
                    if (change >= 1m)
                    {
                        // move stop loss to no loss
                        tradeData.StopPrice = tradeData.BuyPrice + instrument.MinPriceIncrement;//Helpers.RoundPrice(tradeData.BuyPrice * 1.005m, instrument.MinPriceIncrement);
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Moving stop loss to no loss. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                    }
                    else if (change < 0m)
                    {
                        // seems not a Rocket
                        tradeData.StopPrice = candle.Close;
                        tradeData.Time = candle.Time;
                        Logger.Write("{0}: Seems not a Rocket. Set stop loss. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                    }
                }
                else if (tradeData.StopPrice > tradeData.BuyPrice && Helpers.GetChangeInPercent(tradeData.StopPrice, candle.Close) >= 2.0m)
                {
                    // pulling the stop to the price
                    tradeData.StopPrice = Helpers.RoundPrice(candle.Close * 0.99m, instrument.MinPriceIncrement);
                    tradeData.Time = candle.Time;
                    Logger.Write("{0}: Pulling stop loss to current price. Price: {1} Candle: {2}.", instrument.Ticker, tradeData.StopPrice, JsonConvert.SerializeObject(candle));
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "RocketStrategy";
        }
    }
}
