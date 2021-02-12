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


            //Test("BBBY_BBG000CSY9H9_2021-01-21", 1, 0m);
            //Test("OSUR_BBG000J3D1Y8_2021-01-21", 1, 0m);
            //Test("ABNB_BBG001Y2XS07_2021-01-21", 1, 0m);
            //Test("BBBY_BBG000CSY9H9_2021-01-22", 1, 0m);
            //Test("AMCX_BBG000H01H92_2021-01-22", 1, 0m); // -
            //Test("SFIX_BBG0046L1KL9_2021-01-22", 1, 0m);
            //Test("MAC_BBG000BL9C59_2021-01-22", 1, 0m);
            //Test("FANG_BBG002PHSYX9_2021-01-22", 1, 0m);
            //Test("M_BBG000C46HM9_2021-01-25", 1, 0m); // +
            //Test("CNK_BBG000QDVR53_2021-01-25", 1, 0m); // +
            //Test("KR_BBG000BMY992_2021-01-25", 1, 0m); // very good and stable grow
            //Test("MAC_BBG000BL9C59_2021-01-26", 1, 0m);
            //Test("BZUN_BBG008HNS333_2021-01-26", 1, 0m);
            //Test("PSTG_BBG00212PVZ5_2021-02-03", 1, 0m);
            //Test("SAVE_BBG000BF6RQ9_2021-02-03", 1, 0m);
            //Test("MOMO_BBG007HTCQT0_2021-02-03", 1, 0m);
            //Test("ENDP_BBG000C0HQ54_2021-02-02", 1, 0m);
            //Test("MAC_BBG000BL9C59_2021-01-28", 1, 0m);
            //Test("CCL_BBG000BF6LY3_2021-01-28", 1, 0m);
            //Test("SNAP_BBG00441QMJ7_2021-01-28", 1, 0m);
            //Test("", 1, 0m);


            // bad
            //Test("FSLY_BBG004NLQHL0_2021-01-21", 1, 0m); // very interesting... reduce orders and losses
            //Test("HFC_BBG000BL9JQ1_2021-01-26", 1, 0m); // ? 100 quotes ?

            //Test("SPCE_BBG00HTN2CQ3_2021-02-04", 1, 0m);
            //Test("ZGNX_BBG000VDC3G9_2021-02-03", 1, 0m);
            //Test("ENDP_BBG000C0HQ54_2021-02-03", 1, 0m);
            //Test("MYGN_BBG000D9H9F1_2021-02-03", 1, 0m);
            //Test("ARCT_BBG00NNW8JK1_2021-02-02", 1, 0m);
            //Test("CRUS_BBG000C1DHF5_2021-02-02", 1, 0m);
            //Test("VIPS_BBG002NLDLV8_2021-02-02", 1, 0m);
            //Test("GPS_BBG000BKLH74_2021-01-28", 1, 0m);
            //Test("MRNA_BBG003PHHZT1_2021-01-28", 1, 0m);
            //Test("PBF_BBG002832GV8_2021-01-28", 1, 0m);
            //Test("PBI_BBG000BQTMJ9_2021-01-28", 1, 0m);
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
            _writer.WriteLine("_______________POSITIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "Positive\\";

            Test("FATE_BBG000QP35H2_2021-01-07", 2, 5.0m);
            Test("TRIP_BBG001M8HHB7_2021-01-08", 2, 1m);
            Test("CNK_BBG000QDVR53_2020-12-28", 1, 0.85m);
            Test("OIS_BBG000BDDN94_2021-01-14", 1, 0.3m);
            Test("BBBY_BBG000CSY9H9_2021-01-14", 1, 1.6m);
            Test("SFIX_BBG0046L1KL9_2021-01-14", 1, 3.56m);
            Test("CNK_BBG000QDVR53_2021-01-14", 1, 1.31m);
            Test("PBI_BBG000BQTMJ9_2021-01-14", 2, 0.3m);
            Test("ETSY_BBG000N7MXL8_2021-01-14", 1, 8.5m);
            Test("NVTA_BBG005DJFD43_2021-01-14", 1, 1.5m);
            Test("W_BBG001B17MV2_2021-01-14", 2, 40.76m);
            Test("F_BBG000BQPC32_2021-01-14", 1, 0.12m);
            Test("ENPH_BBG001R3MNY9_2021-01-14", 1, 2.29m);
            Test("INTC_BBG000C0G1D1_2021-01-13", 1, 5.5m);
            Test("HALO_BBG000CZ8W54_2021-01-13", 1, 1m);
            Test("BILI_BBG00K7T3037_2021-01-13", 1, 1.5m); // improve
            Test("GM_BBG000NDYB67_2021-01-12", 1, 2.0m);
            Test("EDIT_BBG005MX5GZ2_2021-01-15", 1, 4m);
            Test("NTLA_BBG007KC7PB0_2021-01-15", 1, 4m); // too small quotes?
            Test("ILMN_BBG000DSMS70_2021-01-15", 1, 14.01m);
            Test("LTHM_BBG00LV3NRG0_2021-01-15", 2, 0.46m); //-
            Test("MAC_BBG000BL9C59_2021-01-14", 1, 1.22m);
            Test("W_BBG001B17MV2_2021-01-13", 1, 19.13m);
            Test("MOMO_BBG007HTCQT0_2021-01-13", 2, 0.32m);
            Test("BYND_BBG003CVJP50_2021-01-13", 3, 0.97m); // -
            Test("FTI_BBG00DL8NMV2_2021-01-12", 2, 0.44m);
            Test("EXAS_BBG000CWL0F5_2021-01-11", 1, 10.64m);
            Test("CREE_BBG000BG14P4_2021-01-11", 1, 4.5m);
            Test("DD_BBG00BN961G4_2021-01-11", 1, 3.0m);
            Test("DBX_BBG0018SLDN0_2021-01-11", 1, 1.03m);
            Test("BIIB_BBG000C17X76_2021-01-11", 2, 7.35m);
            Test("PBF_BBG002832GV8_2021-01-11", 2, 0.16m);
            Test("AMD_BBG000BBQCY0_2021-01-11", 1, 3.0m);
            Test("GH_BBG006D97VY9_2021-01-11", 1, 5.99m);
            Test("SPR_BBG000PRJ2Z9_2021-01-11", 1, 1.2m);
            Test("ROKU_BBG001ZZPQJ6_2021-01-11", 1, 9.95m);
            Test("EDIT_BBG005MX5GZ2_2021-01-07", 3, 3.5m); // -
            Test("ROKU_BBG001ZZPQJ6_2021-01-07", 1, 12m);
            Test("BILI_BBG00K7T3037_2021-01-07", 1, 3.88m);
            Test("AMD_BBG000BBQCY0_2021-01-07", 1, 2.5m);
            Test("RDFN_BBG001Q7HP63_2021-01-07", 1, 2.0m);
            Test("NTES_BBG000BX72V8_2021-01-07", 2, 1.5m); // reduce orders count
            Test("UBER_BBG002B04MT8_2021-01-07", 1, 1.0m);
            Test("TDOC_BBG0019T5SG0_2021-01-07", 2, 2.0m); // example of long trend, improve to 1 order
            Test("EDIT_BBG005MX5GZ2_2021-01-06", 1, 7.99m);
            Test("SEDG_BBG0084BBZY6_2021-01-06", 1, 22.45m);
            Test("GE_BBG000BK6MB5_2021-01-06", 1, 0.60m);
            Test("SIG_BBG000C4ZZ10_2021-01-06", 1, 1.44m);
            Test("FITB_BBG000BJL3N0_2021-01-06", 1, 1.1m);
            Test("GPS_BBG000BKLH74_2021-01-06", 1, 0.8m);
            Test("EBAY_BBG000C43RR5_2021-01-06", 1, 1.5m);
            Test("OLLI_BBG0098VVDT9_2021-01-06", 1, 3.44m);
            Test("OMC_BBG000BS9489_2021-01-06", 1, 2.32m);
            Test("TOT_BBG000CHZ857_2021-01-06", 1, 1.4m);
            Test("JWN_BBG000G8N9C6_2021-01-06", 1, 1.0m); // -
            Test("PBCT_BBG000BQT4L6_2021-01-06", 1, 0.42m);
            Test("TDOC_BBG0019T5SG0_2021-01-20", 2, 6.00m);
            Test("DT_BBG00PNN7C40_2021-01-20", 1, 2.11m);
            Test("CHRW_BBG000BTCH57_2021-01-20", 1, 1.29m);
            Test("TAL_BBG0016XJ8S0_2021-01-21", 1, 13.0m);
            Test("MAC_BBG000BL9C59_2021-01-25", 1, 3.47m);
            Test("IRM_BBG000KCZPC3_2021-01-25", 1, 4.0m);
            Test("VIAC_BBG000C496P7_2021-01-25", 1, 4.0m); // +
            Test("OLLI_BBG0098VVDT9_2021-01-25", 1, 8.71m); // +
            Test("URBN_BBG000BL79J3_2021-01-25", 1, 1.75m);
            Test("BYND_BBG003CVJP50_2021-01-25", 1, 8.0m); // +
            Test("BYND_BBG003CVJP50_2021-01-26", 1, 48.87m); // +++
            Test("PBI_BBG000BQTMJ9_2021-01-26", 1, 1.92m); // +++
            Test("TWTR_BBG000H6HNW3_2021-01-26", 1, 2.2m);


            // To improve
            Test("ABNB_BBG001Y2XS07_2021-01-13", 2, 2.8m);
            Test("ZM_BBG0042V6JM8_2021-01-13", 3, 3.17m); // improve SL --> 2 orders
            Test("SNAP_BBG00441QMJ7_2021-01-11", 2, 1.2m); // good deals, but improve closing
            Test("SFIX_BBG0046L1KL9_2021-01-12", 2, 2.33m); // impove SL when good profit

            // TODO tests
            //Test("AAPL_BBG000B9XRY4_2021-01-20", 1, 2.18m);
            //Test("MSFT_BBG000BPH459_2021-01-20", 1, 3.17m);
            //Test("PTON_BBG00JG0FFZ2_2021-01-13", 2, 5.53m); // improve SL --> 1 order


            _basePath = basePath;
        }

        private void RunNegativeTests()
        {
            _writer.WriteLine("_______________NEGATIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "Negative\\";

            Test("INTC_BBG000C0G1D1_2021-01-19", 0, 0m); // market open
            Test("BABA_BBG006G2JVL2_2021-01-19", 0, 0m); // market open
            Test("LITE_BBG0073F9RT7_2021-01-19", 0, 0m); // do not buy after fall
            Test("FDX_BBG000BJF1Z8_2021-01-19", 0, 0m);
            Test("BKR_BBG00GBVBK51_2021-01-21", 0, 0m); // bigg fall
            Test("TFC_BBG000BYYLS8_2021-01-21", 0, 0m);
            Test("SIG_BBG000C4ZZ10_2021-01-25", 0, 0m);
            Test("NVTA_BBG005DJFD43_2021-01-25", 0, 0m);
            Test("NTLA_BBG007KC7PB0_2021-01-26", 0, 0m);
            Test("FANG_BBG002PHSYX9_2021-02-04", 0, 0m);

            Test("EDIT_BBG005MX5GZ2_2021-01-19", 1, -1.5m); // should be minimal losses... improve?!
            Test("JWN_BBG000G8N9C6_2021-01-20", 1, -0.39m); // should be minimal losses
            Test("RRC_BBG000FVXD63_2021-01-21", 1, -0.15m); // minimal losses. improve cosing with minimal losss
            Test("EQT_BBG000BHZ5J9_2021-01-21", 1, -0.3m); // minimize losses
            Test("CHX_BBG00JH9TZ56_2021-02-04", 1, -0.42m); // very bad... to small quotes, do not buy at US open...

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
