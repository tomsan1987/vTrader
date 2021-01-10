﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    public class TradeBotTester
    {
        private ProgramOptions _options;
        private StreamWriter _writer;
        private string _basePath;
        private TradeBot _bot;
        public TradeBotTester(ProgramOptions options)
        {
            _options = options;
        }

        public void Run()
        {
            SetUp();

            // Positive tests
            PositiveTest("FATE_BBG000QP35H2_2021-01-07", 2, 5m);
            PositiveTest("TRIP_BBG001M8HHB7_2021-01-08", 1, 1m);
            PositiveTest("CNK_BBG000QDVR53_2020-12-28", 1, 1m);
            //PositiveTest("", 1, 0m);

            TearDown();
        }

        private void SetUp()
        {
            // create settings
            var settings = new BaseBot.Settings(_options);
            settings.DumpQuotes = false;
            settings.FakeConnection = true;
            settings.RequestCandlesHistory = false;
            settings.Strategies = "ImpulseStrategy";

            // create bot
            _bot = new TradeBot(settings);
            _bot.StartAsync().Wait();

            _basePath = _options.Get<string>("CandlesPath") + "\\";
            if (!Directory.Exists(_basePath))
                throw new Exception("Directory does not exists: " + _basePath);

            _writer = new StreamWriter("test_results_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".log", false);
            _writer.AutoFlush = true;
        }

        private void TearDown()
        {
            _writer.Close();
            _bot.DisposeAsync().AsTask().Wait();
        }

        private void PositiveTest(string testName, int orders, decimal profit)
        {
            _writer.WriteLine(testName);

            try
            {
                var fileName = _basePath + testName + ".csv";
                if (File.Exists(fileName))
                {
                    var candleList = TradeBot.ReadCandles(fileName);
                    var res = _bot.TradeByHistory(candleList);

                    bool passed = (res.totalOrders == orders && res.totalProfit >= profit);
                    if (passed)
                    {
                        _writer.WriteLine("PASSED");
                        _writer.WriteLine("TotalOrders: " + res.totalOrders);
                        _writer.WriteLine("TotalProfit: " + res.totalProfit);
                    }
                    else
                    {
                        _writer.WriteLine("FAILED");
                        _writer.WriteLine("TotalOrders: {0}[{1}]", res.totalOrders, orders);
                        _writer.WriteLine("TotalProfit: {0}[{1}]", res.totalProfit, profit);
                    }
                }
                else
                {
                    _writer.WriteLine("File name {0} not found", fileName);
                }
            }
            catch (Exception e)
            {
                _writer.WriteLine("Exception: " + e.Message);
            }

            _writer.WriteLine("");
        }
    }
}
