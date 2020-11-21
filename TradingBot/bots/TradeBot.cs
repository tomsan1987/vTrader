using System;
using System.IO;
using System.Collections.Generic;
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
    //     Analyzes candles and buy if 3 5m candles closed +0.2%. Stop -1%
    public class TradeBot : BaseBot
    {
        private decimal _totalProfit = 0;
        private int _positiveDeals = 0;
        private int _negativeDeals = 0;
        private int _totalDeals = 0;

        public TradeBot(string token, string config_path) : base(token, config_path)
        {
            Logger.Write("TradeBot created");
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();
        }

        public async Task SaveHistory(DateTime sessionBegin, DateTime sessionEnd)
        {
            Logger.Write("Query candle history...");

            var path = "quote_history\\" + sessionBegin.ToString("yyyy_MM_dd");
            Directory.CreateDirectory(path);

            int idx = 0;
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                bool ok = false;
                while (!ok)
                {
                    try
                    {
                        // query history candles
                        ++idx;
                        var candleList = await _context.MarketCandlesAsync(figi, sessionBegin, sessionEnd, CandleInterval.FiveMinutes);
                        ok = true;

                        TradeByCandles(candleList.Candles);

                        var file = new StreamWriter(path + "\\" + ticker + ".csv", false);
                        file.AutoFlush = true;
                        foreach (var candle in candleList.Candles)
                            file.WriteLine(JsonConvert.SerializeObject(candle));

                    }
                    catch (OpenApiException)
                    {
                        Logger.Write("Context: waiting after {0} queries....", idx);
                        ok = false;
                        idx = 0;
                        await Task.Delay(30000); // sleep for a while
                    }
                    catch (Exception e)
                    {
                        Logger.Write("Excetion: " + e.Message);
                    }
                }
            }

            Logger.Write("Deals(total/pos/neg): {0}/{1}/{2}. Total profit: {3}", _totalDeals, _positiveDeals, _negativeDeals, _totalProfit);
            Logger.Write("Done query candle history...");
        }

        public decimal TradeByHistory(string folderPath)
        {
            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];

                // read history candles
                var filePath = folderPath + "\\" + ticker + ".csv";
                var candleList = ReadCandles(filePath);
                TradeByCandles(candleList);
            }

            Logger.Write("Deals(total/pos/neg): {0}/{1}/{2}. Total profit: {3}", _totalDeals, _positiveDeals, _negativeDeals, _totalProfit);

            return _totalProfit;
        }

        public decimal TradeByCandles(List<CandlePayload> candleList)
        {
            decimal buyPrice = 0;
            decimal stopPrice = 0;
            int positiveCandles = 0;
            foreach (var candle in candleList)
            {
                if (buyPrice != 0)
                {
                    // check if we reach stop conditions
                    if (candle.Low <= stopPrice)
                    {
                        decimal profit = stopPrice - buyPrice;
                        _totalProfit += profit;
                        Logger.Write("Sell one lot {0} by stop loss. Candle time: {1}. Close price: {2}. Profit: {3}({4}%)", _figiToTicker[candle.Figi], candle.Time, candle.Low, profit, Helpers.GetChangeInPercent(buyPrice, stopPrice));
                        if (profit >= 0)
                            ++_positiveDeals;
                        else
                            ++_negativeDeals;

                        // stop loss
                        buyPrice = 0;
                        stopPrice = 0;
                    }
                    else if (Helpers.GetChangeInPercent(buyPrice, candle.Close) > 3)
                    {
                        // move stop loss to no loss
                        stopPrice = Helpers.RoundPrice(buyPrice * (decimal)1.02, _figiToInstrument[candle.Figi].MinPriceIncrement);
                    }
                }

                var change = Helpers.GetChangeInPercent(candle);
                if (change >= (decimal)0.2)
                    positiveCandles++;
                else
                    positiveCandles = 0;

                if (positiveCandles >= 3 && buyPrice == 0)
                {
                    // buy 1 lot
                    Logger.Write("Buy one lot {0}. Candle time: {1}. Buy price: {2}", _figiToTicker[candle.Figi], candle.Time, candle.Close);
                    buyPrice = candle.Close;
                    stopPrice = Helpers.RoundPrice(buyPrice * (decimal)0.99, _figiToInstrument[candle.Figi].MinPriceIncrement);
                    _totalDeals++;
                }
            }

            if (buyPrice != 0)
            {
                // close by last candle
                var c = candleList[candleList.Count - 1];
                decimal profit = c.Close - buyPrice;
                _totalProfit += profit;
                Logger.Write("Sell one lot {0} by end of session. Candle time: {1}. Close price: {2}. Profit: {3}({4}%)", _figiToTicker[c.Figi], c.Time, c.Close, profit, Helpers.GetChangeInPercent(buyPrice, c.Close));

                if (profit >= 0)
                    ++_positiveDeals;
                else
                    ++_negativeDeals;
            }

            return _totalProfit;
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
                    var c = JsonConvert.DeserializeObject<CandlePayload>(line);
                    result.Add(c);
                }
            }

            return result;
        }   

        public override void ShowStatus()
        {

        }
    }
}