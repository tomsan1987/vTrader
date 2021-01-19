using System;
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

        // stat
        private int _totalOrders = 0;
        private decimal _totalProfit = 0;

        public TradeBotTester(ProgramOptions options)
        {
            _options = options;
        }

        public void Run()
        {
            SetUp();
            RunPositiveTests();

            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);
            //PositiveTest("", 1, 0m);


            // TODO
            //PositiveTest("BBBY_BBG000CSY9H9_2021-01-13", 1, 0m);
            //PositiveTest("SPCE_BBG00HTN2CQ3_2021-01-13", 1, 0m); //very intresting... 7 orders
            //PositiveTest("CF_BBG000BWJFZ4_2020-12-28", 1, 1m);
            //PositiveTest("ZYXI_BBG000BJBXZ2_2021-01-14", 1, 0m);
            //PositiveTest("VIPS_BBG002NLDLV8_2021-01-15", 1, 0m); // should not buy?
            //PositiveTest("W_BBG001B17MV2_2021-01-12", 1, 7.87m); // good profit but big degradation in tests

            TearDown();
        }

        private void RunPositiveTests()
        {
            PositiveTest("FATE_BBG000QP35H2_2021-01-07", 2, 5.72m);
            PositiveTest("TRIP_BBG001M8HHB7_2021-01-08", 2, 1m);
            PositiveTest("CNK_BBG000QDVR53_2020-12-28", 2, 0.87m);
            PositiveTest("OIS_BBG000BDDN94_2021-01-14", 1, 0.3m);
            PositiveTest("BBBY_BBG000CSY9H9_2021-01-14", 1, 1.7m);
            PositiveTest("SFIX_BBG0046L1KL9_2021-01-14", 1, 3.56m);
            PositiveTest("CNK_BBG000QDVR53_2021-01-14", 1, 1.31m);
            PositiveTest("PBI_BBG000BQTMJ9_2021-01-14", 2, 0.33m);
            PositiveTest("ETSY_BBG000N7MXL8_2021-01-14", 1, 8.85m);
            PositiveTest("NVTA_BBG005DJFD43_2021-01-14", 2, 0.93m);
            PositiveTest("W_BBG001B17MV2_2021-01-14", 3, 38.03m);
            PositiveTest("F_BBG000BQPC32_2021-01-14", 1, 0.28m);
            PositiveTest("ENPH_BBG001R3MNY9_2021-01-14", 1, 2.29m);
            PositiveTest("INTC_BBG000C0G1D1_2021-01-13", 1, 5.66m);
            PositiveTest("HALO_BBG000CZ8W54_2021-01-13", 2, 1.45m);
            PositiveTest("BILI_BBG00K7T3037_2021-01-13", 1, 4.18m);
            PositiveTest("GM_BBG000NDYB67_2021-01-12", 2, 2.45m);
            PositiveTest("EDIT_BBG005MX5GZ2_2021-01-15", 3, 2.26m);
            PositiveTest("NTLA_BBG007KC7PB0_2021-01-15", 2, 3.26m); // too small quotes?
            PositiveTest("ILMN_BBG000DSMS70_2021-01-15", 1, 14.01m);
            PositiveTest("LTHM_BBG00LV3NRG0_2021-01-15", 2, 0.46m); //-
            PositiveTest("MAC_BBG000BL9C59_2021-01-14", 1, 1.22m);
            PositiveTest("W_BBG001B17MV2_2021-01-13", 1, 19.13m);
            PositiveTest("MOMO_BBG007HTCQT0_2021-01-13", 2, 0.32m);
            PositiveTest("BYND_BBG003CVJP50_2021-01-13", 2, 2.66m); // -
            PositiveTest("FTI_BBG00DL8NMV2_2021-01-12", 2, 0.44m);
            PositiveTest("EXAS_BBG000CWL0F5_2021-01-11", 1, 10.64m);
            PositiveTest("CREE_BBG000BG14P4_2021-01-11", 1, 5.75m);
            PositiveTest("DD_BBG00BN961G4_2021-01-11", 1, 3.79m);
            PositiveTest("DBX_BBG0018SLDN0_2021-01-11", 1, 1.03m);
            PositiveTest("BIIB_BBG000C17X76_2021-01-11", 2, 21.70m);
            PositiveTest("PBF_BBG002832GV8_2021-01-11", 2, 0.20m);
            PositiveTest("AMD_BBG000BBQCY0_2021-01-11", 1, 3.6m);
            PositiveTest("GH_BBG006D97VY9_2021-01-11", 1, 5.99m);
            PositiveTest("SPR_BBG000PRJ2Z9_2021-01-11", 1, 1.41m);
            PositiveTest("ROKU_BBG001ZZPQJ6_2021-01-11", 2, 9.80m);
            PositiveTest("EDIT_BBG005MX5GZ2_2021-01-07", 2, 5.29m); // -

            // To improve
            PositiveTest("ABNB_BBG001Y2XS07_2021-01-13", 2, 2.8m);
            PositiveTest("SFIX_BBG0046L1KL9_2021-01-12", 2, 2.33m); // impove SL when good profit
            PositiveTest("PTON_BBG00JG0FFZ2_2021-01-13", 2, 5.53m); // improve SL --> 1 order
            PositiveTest("ZM_BBG0042V6JM8_2021-01-13", 3, 2.78m); // improve SL --> 2 orders
            PositiveTest("SNAP_BBG00441QMJ7_2021-01-11", 2, 1.51m); // good deals, but improve closing

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
            _writer.WriteLine("\n_____Total_____:");
            _writer.WriteLine("Total orders: " + _totalOrders);
            _writer.WriteLine("Total profit: " + _totalProfit);

            _writer.Close();
            _bot.DisposeAsync().AsTask().Wait();
        }

        private void PositiveTest(string testName, int orders, decimal profit)
        {
            Logger.Write("Test name: " + testName);
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

                    _totalOrders += res.totalOrders;
                    _totalProfit += res.totalProfit;
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
