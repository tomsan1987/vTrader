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
            RunNegativeTests();

            
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("", 1, 0m);
            //Test("TRIP_BBG001M8HHB7_2021-01-19", 1, 0m);
            //



            // TODO
            //Test("UPWK_BBG00FBJ6390_2021-01-19", 1, 0m); // continue following trend if bougth at the end of 5 minutes
            //Test("BBBY_BBG000CSY9H9_2021-01-13", 1, 0m);
            //Test("SPCE_BBG00HTN2CQ3_2021-01-13", 1, 0m); //very intresting... 7 orders
            //Test("CF_BBG000BWJFZ4_2020-12-28", 1, 1m);
            //Test("ZYXI_BBG000BJBXZ2_2021-01-14", 1, 0m);
            //Test("VIPS_BBG002NLDLV8_2021-01-15", 1, 0m); // should not buy?
            //Test("W_BBG001B17MV2_2021-01-12", 1, 7.87m); // good profit but big degradation in tests
            //Test("IOVA_BBG000FTLBV7_2021-01-19", 1, 0m); // bad entry point, do not want to buy
            //Test("TRIP_BBG001M8HHB7_2021-01-19", 1, 0m); // do not want to buy after fall

            TearDown();
        }

        private void RunPositiveTests()
        {
            var basePath = _basePath;
            _basePath += "Positive\\";

            Test("FATE_BBG000QP35H2_2021-01-07", 2, 5.72m);
            Test("TRIP_BBG001M8HHB7_2021-01-08", 2, 1m);
            Test("CNK_BBG000QDVR53_2020-12-28", 1, 0.85m);
            Test("OIS_BBG000BDDN94_2021-01-14", 1, 0.3m);
            Test("BBBY_BBG000CSY9H9_2021-01-14", 1, 1.7m);
            Test("SFIX_BBG0046L1KL9_2021-01-14", 1, 3.56m);
            Test("CNK_BBG000QDVR53_2021-01-14", 1, 1.31m);
            Test("PBI_BBG000BQTMJ9_2021-01-14", 2, 0.33m);
            Test("ETSY_BBG000N7MXL8_2021-01-14", 1, 8.85m);
            Test("NVTA_BBG005DJFD43_2021-01-14", 2, 0.93m);
            Test("W_BBG001B17MV2_2021-01-14", 2, 40.76m);
            Test("F_BBG000BQPC32_2021-01-14", 1, 0.28m);
            Test("ENPH_BBG001R3MNY9_2021-01-14", 1, 2.29m);
            Test("INTC_BBG000C0G1D1_2021-01-13", 1, 5.66m);
            Test("HALO_BBG000CZ8W54_2021-01-13", 2, 1.45m);
            Test("BILI_BBG00K7T3037_2021-01-13", 1, 4.18m);
            Test("GM_BBG000NDYB67_2021-01-12", 1, 2.0m);
            Test("EDIT_BBG005MX5GZ2_2021-01-15", 2, 2.37m);
            Test("NTLA_BBG007KC7PB0_2021-01-15", 1, 4.71m); // too small quotes?
            Test("ILMN_BBG000DSMS70_2021-01-15", 1, 14.01m);
            Test("LTHM_BBG00LV3NRG0_2021-01-15", 2, 0.46m); //-
            Test("MAC_BBG000BL9C59_2021-01-14", 1, 1.22m);
            Test("W_BBG001B17MV2_2021-01-13", 1, 19.13m);
            Test("MOMO_BBG007HTCQT0_2021-01-13", 2, 0.32m);
            Test("BYND_BBG003CVJP50_2021-01-13", 3, 0.97m); // -
            Test("FTI_BBG00DL8NMV2_2021-01-12", 2, 0.44m);
            Test("EXAS_BBG000CWL0F5_2021-01-11", 1, 10.64m);
            Test("CREE_BBG000BG14P4_2021-01-11", 1, 5.75m);
            Test("DD_BBG00BN961G4_2021-01-11", 1, 3.79m);
            Test("DBX_BBG0018SLDN0_2021-01-11", 1, 1.03m);
            Test("BIIB_BBG000C17X76_2021-01-11", 2, 7.35m);
            Test("PBF_BBG002832GV8_2021-01-11", 2, 0.16m);
            Test("AMD_BBG000BBQCY0_2021-01-11", 1, 3.6m);
            Test("GH_BBG006D97VY9_2021-01-11", 1, 5.99m);
            Test("SPR_BBG000PRJ2Z9_2021-01-11", 1, 1.41m);
            Test("ROKU_BBG001ZZPQJ6_2021-01-11", 2, 9.95m);
            Test("EDIT_BBG005MX5GZ2_2021-01-07", 3, 4.04m); // -
            Test("ROKU_BBG001ZZPQJ6_2021-01-07", 2, 14.27m);
            Test("BILI_BBG00K7T3037_2021-01-07", 1, 3.88m);
            Test("AMD_BBG000BBQCY0_2021-01-07", 1, 3.30m);
            Test("RDFN_BBG001Q7HP63_2021-01-07", 1, 2.17m);
            Test("NTES_BBG000BX72V8_2021-01-07", 3, 3.32m); // reduce orders count
            Test("UBER_BBG002B04MT8_2021-01-07", 1, 1.21m);
            Test("TDOC_BBG0019T5SG0_2021-01-07", 3, 2.53m); // example of long trend, improve to 1 order
            Test("EDIT_BBG005MX5GZ2_2021-01-06", 1, 7.99m);
            Test("SEDG_BBG0084BBZY6_2021-01-06", 1, 22.45m);
            Test("GE_BBG000BK6MB5_2021-01-06", 1, 0.60m);
            Test("SIG_BBG000C4ZZ10_2021-01-06", 1, 1.44m);
            Test("FITB_BBG000BJL3N0_2021-01-06", 1, 1.35m);
            Test("GPS_BBG000BKLH74_2021-01-06", 1, 0.89m);
            Test("EBAY_BBG000C43RR5_2021-01-06", 1, 2.12m);
            Test("OLLI_BBG0098VVDT9_2021-01-06", 1, 3.44m);
            Test("OMC_BBG000BS9489_2021-01-06", 1, 2.32m);
            Test("TOT_BBG000CHZ857_2021-01-06", 1, 1.58m);
            Test("JWN_BBG000G8N9C6_2021-01-06", 1, 1.15m);
            Test("PBCT_BBG000BQT4L6_2021-01-06", 1, 0.42m);
            Test("TDOC_BBG0019T5SG0_2021-01-20", 2, 6.00m);
            Test("DT_BBG00PNN7C40_2021-01-20", 1, 2.11m);
            Test("AAPL_BBG000B9XRY4_2021-01-20", 1, 2.18m);
            Test("MSFT_BBG000BPH459_2021-01-20", 1, 3.17m);
            Test("CHRW_BBG000BTCH57_2021-01-20", 1, 1.29m);


            // To improve
            Test("ABNB_BBG001Y2XS07_2021-01-13", 2, 2.8m);
            Test("SFIX_BBG0046L1KL9_2021-01-12", 2, 2.33m); // impove SL when good profit
            Test("PTON_BBG00JG0FFZ2_2021-01-13", 2, 5.53m); // improve SL --> 1 order
            Test("ZM_BBG0042V6JM8_2021-01-13", 3, 3.17m); // improve SL --> 2 orders
            Test("SNAP_BBG00441QMJ7_2021-01-11", 2, 1.51m); // good deals, but improve closing

            _basePath = basePath;
        }

        private void RunNegativeTests()
        {
            var basePath = _basePath;
            _basePath += "Negative\\";

            Test("INTC_BBG000C0G1D1_2021-01-19", 0, 0m); // market open
            Test("BABA_BBG006G2JVL2_2021-01-19", 0, 0m); // market open
            Test("LITE_BBG0073F9RT7_2021-01-19", 0, 0m); // do not buy after fall

            Test("FDX_BBG000BJF1Z8_2021-01-19", 1, -0.43m); // should be minimal losses
            Test("EDIT_BBG005MX5GZ2_2021-01-19", 1, -1.14m); // should be minimal losses... improve?!
            Test("JWN_BBG000G8N9C6_2021-01-20", 1, -0.39m); // should be minimal losses

            _basePath = basePath;
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

        private void Test(string testName, int orders, decimal profit)
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
