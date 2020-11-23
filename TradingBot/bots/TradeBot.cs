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
        private decimal _profitRub = 0;
        private decimal _profitUsd = 0;
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

                        var file = new StreamWriter(path + "\\" + ticker + ".csv", false);
                        file.AutoFlush = true;
                        foreach (var candle in candleList.Candles)
                            file.WriteLine(JsonConvert.SerializeObject(candle));

                        file.Close();

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

            Logger.Write("Done query candle history...");
        }

        public decimal TradeByHistory(string folderPath, string tickets = "")
        {
            Reset();

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
                TradeByCandles(candleList);
            }

            Logger.Write("Deals(total/pos/neg): {0}/{1}/{2}. Total profit(Usd/Rub): {3}/{4}", _totalDeals, _positiveDeals, _negativeDeals, _profitUsd, _profitRub);

            return 0;
        }

        public decimal TradeByCandles(List<CandlePayload> candleList)
        {
            decimal buyPrice = 0;
            decimal stopPrice = 0;
            int positiveCandles = 0;
            decimal momentumChange = 0;
            decimal startPrice = 0;
            for (int i = 0; i < candleList.Count; ++i)
            {
                var candle = candleList[i];

                // ignore candles with low volume
                if (candle.Volume < 100)
                    continue;

                if (buyPrice != 0)
                {
                    // check if we reach stop conditions
                    if (candle.Low <= stopPrice)
                    {
                        decimal profit = stopPrice - buyPrice;
                        if (_figiToInstrument[candle.Figi].Currency == Currency.Rub)
                            _profitRub += profit;
                        else if (_figiToInstrument[candle.Figi].Currency == Currency.Usd)
                            _profitUsd += profit;
                        else
                            throw new Exception("Unknown currency for " + _figiToInstrument[candle.Figi].Ticker);

                        Logger.Write("Sell one lot {0} by stop loss. Candle time: {1}. Close price: {2}. Profit: {3}({4}%)", _figiToTicker[candle.Figi], candle.Time, stopPrice, profit, Helpers.GetChangeInPercent(buyPrice, stopPrice));
                        if (profit >= 0)
                            ++_positiveDeals;
                        else
                            ++_negativeDeals;

                        // stop loss
                        buyPrice = 0;
                        stopPrice = 0;
                    }
                    else if (stopPrice < buyPrice && Helpers.GetChangeInPercent(buyPrice, candle.Close) >= (decimal)0.005)
                    {
                        // move stop loss to no loss
                        stopPrice = Helpers.RoundPrice(buyPrice * (decimal)1.005, _figiToInstrument[candle.Figi].MinPriceIncrement);
                        Logger.Write("Moving stop loss {0} to no loss. Price: {1} Candle time: {2}.", _figiToTicker[candle.Figi], stopPrice, candle.Time);
                    }
                    else if (stopPrice > buyPrice && Helpers.GetChangeInPercent(stopPrice, candle.Close) >= (decimal)0.005)
                    {
                        // pulling the stop to the price
                        stopPrice = Helpers.RoundPrice(candle.Close * (decimal)0.995, _figiToInstrument[candle.Figi].MinPriceIncrement);
                        Logger.Write("Pulling stop loss {0} to current price. Price: {1} Candle time: {2}.", _figiToTicker[candle.Figi], stopPrice, candle.Time);
                    }
                }

                var change = Helpers.GetChangeInPercent(candle);
                if (change >= (decimal)0.2)
                {
                    positiveCandles++;
                    momentumChange += change;
                    if (startPrice == 0)
                        startPrice = candle.Open;
                }
                else
                {
                    positiveCandles = 0;
                    momentumChange = 0;
                    startPrice = 0;
                }

                // checked that it is not grow afret fall
                decimal localChange = 0;
                int j = Math.Max(0, i - positiveCandles - 10);
                if (i - positiveCandles > 0)
                    localChange = Helpers.GetChangeInPercent(candleList[j].Close, candleList[i - positiveCandles].Close);

                if (buyPrice == 0)
                {
                    if (localChange >= 0 && positiveCandles >= 3)
                    {
                        // buy 1 lot
                        Logger.Write("Buy one lot {0}. Candle time: {1}. Buy price: {2}. Details: localCahnge {3}, positiveCandles {4}", _figiToTicker[candle.Figi], candle.Time, candle.Close, localChange, positiveCandles);
                        buyPrice = candle.Close;
                        stopPrice = Helpers.RoundPrice(buyPrice * (decimal)0.99, _figiToInstrument[candle.Figi].MinPriceIncrement);
                        _totalDeals++;
                    }
                    else if (momentumChange >= 1 && (localChange >= 0 || momentumChange >= 2 * Math.Abs(localChange)))
                    {
                        // buy 1 lot
                        buyPrice = Helpers.RoundPrice(startPrice * (decimal)1.01, _figiToInstrument[candle.Figi].MinPriceIncrement);
                        Logger.Write("Buy one lot {0}. Candle time: {1}. Buy price: {2}. Details: momentumChange {3}, localChange {4}", _figiToTicker[candle.Figi], candle.Time, buyPrice, momentumChange, localChange);
                        stopPrice = Helpers.RoundPrice(buyPrice * (decimal)0.99, _figiToInstrument[candle.Figi].MinPriceIncrement);
                        _totalDeals++;
                    }
                }
            }

            if (buyPrice != 0)
            {
                // close by last candle
                var candle = candleList[candleList.Count - 1];
                decimal profit = candle.Close - buyPrice;
                if (_figiToInstrument[candle.Figi].Currency == Currency.Rub)
                    _profitRub += profit;
                else if (_figiToInstrument[candle.Figi].Currency == Currency.Usd)
                    _profitUsd += profit;
                else
                    throw new Exception("Unknown currency for " + _figiToInstrument[candle.Figi].Ticker);

                Logger.Write("Sell one lot {0} by end of session. Candle time: {1}. Close price: {2}. Profit: {3}({4}%)", _figiToTicker[candle.Figi], candle.Time, candle.Close, profit, Helpers.GetChangeInPercent(buyPrice, candle.Close));

                if (profit >= 0)
                    ++_positiveDeals;
                else
                    ++_negativeDeals;
            }

            return 0;
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

        private void Reset()
        {
            _profitUsd = 0;
            _profitRub = 0;
            _positiveDeals = 0;
            _negativeDeals = 0;
            _totalDeals = 0;
        }
    }
}