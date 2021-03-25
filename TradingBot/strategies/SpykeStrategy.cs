using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class SpykeStrategy : IStrategy
    {
        public IStrategy.StrategyResultType Process(MarketInstrument instrument, TradeData tradeData, Quotes quotes)
        {
            var candles = quotes.Candles;
            var candle = candles[candles.Count - 1];

            if (tradeData.Status == Status.Watching)
            {
                if (candles.Count < 3)
                    return IStrategy.StrategyResultType.NoOp;

                decimal change = 0;
                if (quotes.IsSpike(out change) && candle.Close < candle.Open && change < 0)
                {
                    // spike detected try to buy
                    tradeData.StrategyData = new SpykeStrategyData();
                    tradeData.BuyPrice = candle.Close + 2 * instrument.MinPriceIncrement;
                    Logger.Write("{0}: BuyPending. Strategy: {1}. Price: {2}. Candle: {3}", instrument.Ticker, Description(), tradeData.BuyPrice, JsonConvert.SerializeObject(candle));
                    return IStrategy.StrategyResultType.Buy;
                }

                return IStrategy.StrategyResultType.NoOp;
            }
            else if (tradeData.Status == Status.BuyPending)
            {
                // if we got some quotes after spike but order still not executed - cancel it.
                var strategyData = tradeData.StrategyData as SpykeStrategyData;
                ++strategyData.QuotesBuy;
                if (strategyData.QuotesBuy > 4)
                {
                    tradeData.StrategyData = null;
                    Logger.Write("{0}: No lack with spike, cancel order....", instrument.Ticker);
                    return IStrategy.StrategyResultType.CancelOrder;
                }
            }
            else if (tradeData.Status == Status.BuyDone)
            {
                if (candle.Close > tradeData.BuyPrice)
                {
                    tradeData.StrategyData = null;
                    tradeData.SellPrice = candle.Close;
                    Logger.Write("{0}: Was bought by spike. Pending. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, tradeData.SellPrice, JsonConvert.SerializeObject(candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                    return IStrategy.StrategyResultType.Sell;
                }
                else
                {
                    var strategyData = tradeData.StrategyData as SpykeStrategyData;
                    ++strategyData.QuotesSell;

                    if (strategyData.QuotesSell > 5)
                    {
                        tradeData.StrategyData = null;
                        tradeData.SellPrice = candle.Close;
                        Logger.Write("{0}: Something went wrong. Closing. Close price: {1}. Candle: {2}. Profit: {3}({4}%)", instrument.Ticker, tradeData.SellPrice, JsonConvert.SerializeObject(candle), tradeData.SellPrice - tradeData.BuyPrice, Helpers.GetChangeInPercent(tradeData.BuyPrice, tradeData.SellPrice));
                        return IStrategy.StrategyResultType.Sell;
                    }
                }
            }

            return IStrategy.StrategyResultType.NoOp;
        }

        public string Description()
        {
            return "SpykeStrategy";
        }
    }

    public class SpykeStrategyData : IStrategyData
    {
        public int QuotesBuy { get; set; }
        public int QuotesSell { get; set; }
    }
}
