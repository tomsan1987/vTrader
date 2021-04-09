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
        private string _strategy;
        private string _testNameFilter = ""; // will run only test with specified name(for debug purpose only)

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

            if (_strategy == "MorningOpenStrategy")
            {
                TestMorningOpenStrategy();
            }
            else if (_strategy == "ImpulseStrategy")
            {
                TestImpulseStrategy();
            }

            TearDown();
        }

        private void RunPositiveTestsImpulseStrategy()
        {
            _writer.WriteLine("_______________POSITIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "Positive\\";

            Test("CF_BBG000BWJFZ4_2020-12-28", 1, 0.4m);
            Test("FATE_BBG000QP35H2_2021-01-07", 1, 4.0m);
            Test("TRIP_BBG001M8HHB7_2021-01-08", 2, 1m);
            Test("CNK_BBG000QDVR53_2020-12-28", 1, 0.85m);
            Test("OIS_BBG000BDDN94_2021-01-14", 1, 0.3m);
            Test("BBBY_BBG000CSY9H9_2021-01-14", 1, 1.6m);
            Test("SFIX_BBG0046L1KL9_2021-01-14", 1, 3.56m);
            Test("CNK_BBG000QDVR53_2021-01-14", 1, 1.31m);
            Test("PBI_BBG000BQTMJ9_2021-01-14", 2, 0.3m);
            Test("ETSY_BBG000N7MXL8_2021-01-14", 1, 8.5m);
            Test("NVTA_BBG005DJFD43_2021-01-14", 1, 1.5m);
            Test("W_BBG001B17MV2_2021-01-14", 1, 40.76m); // possible 2 orders
            Test("F_BBG000BQPC32_2021-01-14", 1, 0.12m);
            Test("ENPH_BBG001R3MNY9_2021-01-14", 1, -0.5m); // improve closing when price is growing
            Test("INTC_BBG000C0G1D1_2021-01-13", 1, 5.5m);
            Test("HALO_BBG000CZ8W54_2021-01-13", 1, 1m);
            Test("BILI_BBG00K7T3037_2021-01-13", 1, 1.5m); // improve
            Test("GM_BBG000NDYB67_2021-01-12", 1, 2.0m);
            Test("EDIT_BBG005MX5GZ2_2021-01-15", 1, 4m);
            Test("NTLA_BBG007KC7PB0_2021-01-15", 1, 2.7m); // too small quotes?
            Test("ILMN_BBG000DSMS70_2021-01-15", 1, 14.01m);
            Test("LTHM_BBG00LV3NRG0_2021-01-15", 2, 0.2m); //-
            Test("MAC_BBG000BL9C59_2021-01-14", 1, 1.22m);
            Test("W_BBG001B17MV2_2021-01-13", 1, 19.13m);
            Test("MOMO_BBG007HTCQT0_2021-01-13", 2, 0.25m);
            Test("BYND_BBG003CVJP50_2021-01-13", 3, -0.1m); // -
            Test("FTI_BBG00DL8NMV2_2021-01-12", 2, 0.44m);
            Test("EXAS_BBG000CWL0F5_2021-01-11", 1, 10.64m);
            Test("CREE_BBG000BG14P4_2021-01-11", 1, 4m);
            Test("DD_BBG00BN961G4_2021-01-11", 1, 3.0m);
            Test("DBX_BBG0018SLDN0_2021-01-11", 1, 0.9m);
            Test("BIIB_BBG000C17X76_2021-01-11", 2, 7.35m);
            Test("PBF_BBG002832GV8_2021-01-11", 1, 0.25m);
            Test("AMD_BBG000BBQCY0_2021-01-11", 1, 3.0m);
            Test("GH_BBG006D97VY9_2021-01-11", 1, 4m);
            Test("SPR_BBG000PRJ2Z9_2021-01-11", 1, 1.2m);
            Test("ROKU_BBG001ZZPQJ6_2021-01-11", 1, 9.95m);
            Test("EDIT_BBG005MX5GZ2_2021-01-07", 1, 3.5m); // -
            Test("ROKU_BBG001ZZPQJ6_2021-01-07", 1, 10m);
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
            Test("TDOC_BBG0019T5SG0_2021-01-20", 2, 3.7m); // possible candidate for TP
            Test("DT_BBG00PNN7C40_2021-01-20", 1, 2.11m);
            Test("CHRW_BBG000BTCH57_2021-01-20", 1, 0.8m);
            Test("TAL_BBG0016XJ8S0_2021-01-21", 1, 13.0m);
            Test("MAC_BBG000BL9C59_2021-01-25", 1, 3.47m);
            Test("IRM_BBG000KCZPC3_2021-01-25", 1, 4.0m);
            Test("VIAC_BBG000C496P7_2021-01-25", 1, 4.0m); // +
            Test("OLLI_BBG0098VVDT9_2021-01-25", 1, 8.71m); // +
            Test("URBN_BBG000BL79J3_2021-01-25", 1, 1.7m);
            Test("BYND_BBG003CVJP50_2021-01-25", 1, 8.0m); // +
            Test("BYND_BBG003CVJP50_2021-01-26", 1, 48.87m); // +++
            Test("PBI_BBG000BQTMJ9_2021-01-26", 1, 1.92m); // +++
            Test("TWTR_BBG000H6HNW3_2021-01-26", 1, 2.2m);
            Test("BBBY_BBG000CSY9H9_2021-01-21", 1, 2m);
            Test("OSUR_BBG000J3D1Y8_2021-01-21", 1, 0.8m); // good. possible second order
            Test("BBBY_BBG000CSY9H9_2021-01-22", 2, 2m); // +-
            Test("AMCX_BBG000H01H92_2021-01-22", 2, 3m);
            Test("M_BBG000C46HM9_2021-01-25", 2, 1.5m); // +
            Test("SNAP_BBG00441QMJ7_2021-01-28", 1, 3m);
            Test("CCL_BBG000BF6LY3_2021-01-28", 2, 2m); // +. improve me: do not but first order: not enough quotes?
            Test("ENDP_BBG000C0HQ54_2021-02-02", 1, 0.4m);
            Test("MOMO_BBG007HTCQT0_2021-02-03", 2, 0.35m);
            Test("SAVE_BBG000BF6RQ9_2021-02-03", 1, 1.2m);
            Test("PSTG_BBG00212PVZ5_2021-02-03", 1, 1.5m);
            Test("FANG_BBG002PHSYX9_2021-01-22", 1, 1.5m);
            Test("MAC_BBG000BL9C59_2021-01-22", 3, 0.3m); // may be improved
            Test("UPWK_BBG00FBJ6390_2021-01-19", 2, 0.0m); // -
            Test("FSLY_BBG004NLQHL0_2021-01-21", 2, 1.5m); // +-
            Test("SPCE_BBG00HTN2CQ3_2021-01-13", 3, -0.1m); // 1+, 2-
            Test("PBI_BBG000BQTMJ9_2021-01-28", 1, 0.2m);
            Test("ARCT_BBG00NNW8JK1_2021-02-02", 1, 2.5m);

            // To improve
            Test("ABNB_BBG001Y2XS07_2021-01-13", 1, 2.8m);
            Test("ZM_BBG0042V6JM8_2021-01-13", 3, 2.15m); // improve SL --> 2 orders
            Test("SNAP_BBG00441QMJ7_2021-01-11", 2, 1.2m); // good deals, but improve closing
            Test("SFIX_BBG0046L1KL9_2021-01-12", 1, 2.33m); // improve SL when good profit
            Test("MRNA_BBG003PHHZT1_2021-01-28", 4, 2.9m); // reduce orders count
            Test("ABNB_BBG001Y2XS07_2021-01-21", 1, 8m); // reduce orders count
            Test("CNK_BBG000QDVR53_2021-01-25", 1, 0m); // improve EXIT: losses 7% of profit! improve buy?
            Test("BZUN_BBG008HNS333_2021-01-26", 2, 1.9m); // TODO: do not measure fall from far maximum -> much better profit
            Test("W_BBG001B17MV2_2021-01-12", 1, 0.7m); // entry point could be improved =? profit > 7$
            Test("SPCE_BBG00HTN2CQ3_2021-02-04", 2, 0.4m); // profit could be much bigger...


            // TODO tests

            //Test("MSFT_BBG000BPH459_2021-01-20", 1, 3.17m);
            //Test("PTON_BBG00JG0FFZ2_2021-01-13", 2, 5.53m); // improve SL --> 1 order
            //Test("KR_BBG000BMY992_2021-01-25", 1, 0m); // very good and stable grow. good trend, small fall, but candles is not so big

            _basePath = basePath;
        }

        private void RunNegativeTestsImpulseStrategy()
        {
            _writer.WriteLine("_______________NEGATIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "Negative\\";

            Test("INTC_BBG000C0G1D1_2021-01-19", 0, 0m); // market open
            Test("BABA_BBG006G2JVL2_2021-01-19", 0, 0m); // market open
            Test("LITE_BBG0073F9RT7_2021-01-19", 0, 0m); // do not buy after fall
            Test("FDX_BBG000BJF1Z8_2021-01-19", 0, 0m);
            Test("BKR_BBG00GBVBK51_2021-01-21", 0, 0m); // big fall
            Test("TFC_BBG000BYYLS8_2021-01-21", 0, 0m);
            Test("SIG_BBG000C4ZZ10_2021-01-25", 0, 0m);
            Test("NVTA_BBG005DJFD43_2021-01-25", 0, 0m);
            Test("NTLA_BBG007KC7PB0_2021-01-26", 0, 0m);
            Test("FANG_BBG002PHSYX9_2021-02-04", 0, 0m);
            Test("ZGNX_BBG000VDC3G9_2021-02-03", 0, 0m); // do not trade on spikes(high volatile +-5-6%)
            Test("VIPS_BBG002NLDLV8_2021-01-15", 0, 0m); // should not buy. reason - ?
            Test("IOVA_BBG000FTLBV7_2021-01-19", 0, 0m); // do not buy!


            Test("EDIT_BBG005MX5GZ2_2021-01-19", 1, -1.6m); // should be minimal losses... improve?!
            Test("JWN_BBG000G8N9C6_2021-01-20", 1, -0.39m); // should be minimal losses
            Test("RRC_BBG000FVXD63_2021-01-21", 1, -0.15m); // minimal losses. improve cosing with minimal loss
            Test("EQT_BBG000BHZ5J9_2021-01-21", 1, -0.3m); // minimize losses
            Test("CHX_BBG00JH9TZ56_2021-02-04", 1, -0.42m); // very bad... to small quotes, do not buy at US open...
            Test("CRUS_BBG000C1DHF5_2021-02-02", 1, -2.6m); // too bad trend...
            Test("VIPS_BBG002NLDLV8_2021-02-02", 1, -0.7m); // buy on the high...
            Test("TRIP_BBG001M8HHB7_2021-01-19", 1, -0.4m); // will be good to not buy. how?
            Test("HFC_BBG000BL9JQ1_2021-01-26", 1, -0.7m); // will be good to do not by it. ? 100 quotes ?
            Test("ENDP_BBG000C0HQ54_2021-02-03", 1, -0.3m); // not enough quotes?
            Test("GPS_BBG000BKLH74_2021-01-28", 1, -1m); // 4% loss, someone sell by market price, try to average
            Test("EDIT_BBG005MX5GZ2_2020-12-30", 1, -2.5m); // will be good to not buy
            Test("PBF_BBG002832GV8_2020-12-08", 1, -0.2m); // just minimize losses


            _basePath = basePath;
        }

        private void RunPositiveTestsMorningOpenStrategy()
        {
            _writer.WriteLine("_______________POSITIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "Positive\\";

            Test("SPCE_BBG00HTN2CQ3_2021-01-28", 1, 3.0m); // just lucky now
            Test("SPCE_BBG00HTN2CQ3_2020-12-14", 2, 0.4m); // may be improve buy and sell
            Test("SPCE_BBG00HTN2CQ3_2021-02-02", 1, 2.5m);
            Test("ZYXI_BBG000BJBXZ2_2021-01-28", 1, 0.5m);
            Test("SWBI_BBG000BM0QL7_2020-12-04", 1, 0.2m);
            Test("NTNX_BBG001NDW1Z7_2021-01-06", 1, 0.0m); // OK if not buy
            Test("MRNA_BBG003PHHZT1_2021-01-19", 1, 0.0m); // TODO: stat calculated not correct
            Test("SEDG_BBG0084BBZY6_2021-01-25", 1, 7.0m); // mega positive
            Test("BILI_BBG00K7T3037_2021-01-27", 1, 2.0m);
            Test("NTLA_BBG007KC7PB0_2021-01-27", 1, 1.0m);
            Test("MFGP_BBG00HFWVGN0_2020-12-04", 1, 0.1m);
            Test("CCL_BBG000BF6LY3_2021-02-03", 1, 0.2m);
            Test("OXY_BBG000BQQ2S6_2020-12-14", 1, 0.25m);
            Test("AAL_BBG005P7Q881_2020-12-16", 1, 0.2m);
            Test("EQT_BBG000BHZ5J9_2021-01-19", 1, 0.05m); // improve me when low quotes count
            Test("PBF_BBG002832GV8_2021-01-25", 1, 0.15m);
            Test("CCL_BBG000BF6LY3_2020-12-16", 1, 0.2m);
            Test("CRM_BBG000BN2DC2_2020-12-21", 1, 1.5m);
            Test("MFGP_BBG00HFWVGN0_2020-12-10", 1, 0.06m);
            Test("BLUE_BBG000QGWY50_2021-01-19", 1, 0.2m);
            Test("PFE_BBG000BR2B91_2020-12-04", 1, 0.1m); // improve me: many quotes per stat
            Test("ATRA_BBG005Q3MQY4_2021-01-27", 1, 0.2m);
            Test("SPCE_BBG00HTN2CQ3_2020-12-11", 1, 0.5m);
            Test("EQT_BBG000BHZ5J9_2020-12-04", 1, 0.1m);
            Test("AA_BBG00B3T3HD3_2021-01-28", 1, 0.01m); // +-
            Test("MFGP_BBG00HFWVGN0_2020-12-09", 1, 0.05m); // may be improved. gap down and price falling
            Test("ETRN_BBG00K53L394_2021-02-02", 1, 0.0m); // little profit
            Test("SPCE_BBG00HTN2CQ3_2021-01-22", 1, 0.5m);
            Test("TSLA_BBG000N9MNX3_2021-01-28", 1, 5.0m);
            Test("ZYXI_BBG000BJBXZ2_2021-01-22", 1, 0.0m); // it is good that we have small profit here and sold at time
            Test("SPCE_BBG00HTN2CQ3_2021-01-26", 1, 0.0m); // improve me: if the next candle ha not significant change - do not close
            Test("DKNG_BBG00TCBG714_2021-01-28", 2, 0.2m); // can be improved to buy by statistic. // there was not enough liquidity when buy > 1 lots
            Test("PINS_BBG002583CV8_2021-01-27", 1, 2.0m); // +
            Test("ETRN_BBG00K53L394_2021-01-27", 1, 0.08m);
            Test("BBBY_BBG000CSY9H9_2021-02-04", 1, 0.4m);
            Test("F_BBG000BQPC32_2021-01-28", 1, 0.01m); // little profit
            Test("PINS_BBG002583CV8_2021-02-02", 1, 0.5m);
            Test("SPCE_BBG00HTN2CQ3_2021-01-25", 1, 0.05m);
            Test("GE_BBG000BK6MB5_2021-01-26", 1, 0.02m);
            Test("CHEF_BBG001MFW6D6_2020-12-14", 2, 0.90m);
            Test("COTY_BBG000F395V1_2021-01-26", 1, 0.05m);
            Test("ZGNX_BBG000VDC3G9_2021-01-22", 1, 0.05m); // +-
            Test("SWBI_BBG000BM0QL7_2020-12-16", 1, 0.1m);
            Test("ZYXI_BBG000BJBXZ2_2021-01-25", 1, 0.01m); // could be improved?
            Test("SPCE_BBG00HTN2CQ3_2020-12-10", 2, 0.30m);
            Test("TSLA_BBG000N9MNX3_2020-12-09", 1, 2.0m);
            Test("ARCT_BBG00NNW8JK1_2021-02-03", 1, 1.0m); // big gap up, price is close to prev day close
            Test("TSLA_BBG000N9MNX3_2020-12-10", 1, 8.0m);
            Test("PBF_BBG002832GV8_2020-12-21", 1, 0.10m);
            Test("DKNG_BBG00TCBG714_2020-12-09", 1, 0.20m);
            Test("ABNB_BBG001Y2XS07_2021-01-22", 1, 0.40m);
            Test("DBX_BBG0018SLDN0_2020-12-14", 1, 0.20m);
            Test("TSLA_BBG000N9MNX3_2020-12-21", 1, 0.09m); // good to have no lose here...
            Test("MAC_BBG000BL9C59_2021-01-20", 1, 0.0m);
            Test("MFGP_BBG00HFWVGN0_2021-01-28", 1, 0.0m);
            Test("MAC_BBG000BL9C59_2021-02-04", 1, 0.0m);
            Test("ZYXI_BBG000BJBXZ2_2020-12-21", 1, 0.0m);
            Test("EQT_BBG000BHZ5J9_2020-12-07", 1, 0.0m);
            Test("PBF_BBG002832GV8_2021-01-19", 1, 0.02m);
            Test("PBF_BBG002832GV8_2020-12-14", 1, 0.05m);
            Test("NET_BBG001WMKHH5_2021-01-27", 1, 3.0m); //++
            Test("PBI_BBG000BQTMJ9_2021-01-28", 1, 0.05m);
            Test("SPCE_BBG00HTN2CQ3_2020-12-21", 2, 0.01m); // buy more!
            Test("ICPT_BBG001J1QN87_2021-03-15", 1, 1.0m);
            Test("MFGP_BBG00HFWVGN0_2021-03-04", 1, 0.2m);
            Test("GILD_BBG000CKGBP2_2021-03-15", 1, 1.5m);
            Test("BILI_BBG00K7T3037_2021-03-05", 1, 3.0m);
            Test("AA_BBG00B3T3HD3_2021-03-04", 1, 0.80m);
            Test("SNAP_BBG00441QMJ7_2021-02-24", 1, 1.5m);
            Test("FTI_BBG00DL8NMV2_2021-02-25", 1, 0.18m);
            Test("T_BBG000BSJK37_2021-03-08", 1, 0.7m);
            Test("APA_BBG00YTS96G2_2021-03-11", 1, 0.5m);
            Test("ACH_BBG000CMRVH1_2021-02-24", 1, 0.3m);
            Test("BLUE_BBG000QGWY50_2021-03-12", 1, 0.7m); // real order
            Test("SQ_BBG0018SLC07_2021-03-03", 1, 5.0m);
            Test("ARCT_BBG00NNW8JK1_2021-02-24", 1, 1.0m);
            Test("MRNA_BBG003PHHZT1_2021-02-26", 1, 3.0m);
            Test("ET_BBG000BM2FL9_2021-02-18", 1, 0.10m);
            Test("COG_BBG000C3GN47_2021-03-12", 1, 0.3m);
            Test("TPR_BBG000BY29C7_2021-02-18", 1, 0.7m);
            Test("COTY_BBG000F395V1_2021-02-25", 1, 0.14m);
            Test("FSLY_BBG004NLQHL0_2021-03-05", 1, 1.0m);
            Test("SDGR_BBG000T88BN2_2021-03-12", 1, 1.0m);
            Test("BABA_BBG006G2JVL2_2021-03-12", 1, 4.0m);
            Test("PINS_BBG002583CV8_2021-02-24", 1, 1.0m);
            Test("NVDA_BBG000BBJQV0_2021-02-26", 1, 8.0m);
            Test("CRTX_BBG00BTK1DT8_2021-03-11", 1, 0.5m);
            Test("ENPH_BBG001R3MNY9_2021-03-04", 1, 1.5m);
            Test("JD_BBG005YHY0Q7_2021-03-12", 1, 0.8m);
            Test("AMD_BBG000BBQCY0_2021-02-26", 1, 0.7m);
            Test("MOMO_BBG007HTCQT0_2021-02-26", 1, 0.1m);
            Test("ENDP_BBG000C0HQ54_2021-02-26", 1, 0.05m);
            Test("GT_BBG000BKNX95_2021-03-01", 1, 0.05m);
            Test("CCL_BBG000BF6LY3_2021-02-26", 1, 0.07m);
            Test("UAL_BBG000M65M61_2021-02-26", 2, 0.3m); // TODO: buy more!
            Test("BYND_BBG003CVJP50_2021-02-24", 1, 0.2m); // big gap up
            Test("ACH_BBG000CMRVH1_2021-04-09", 2, 0.2m);


            _basePath = basePath;
        }

        private void RunNegativeTestsMorningOpenStrategy()
        {
            _writer.WriteLine("_______________NEGATIVE TESTS_______________");

            var basePath = _basePath;
            _basePath += "NEGATIVE\\";

            // improved tests!
            Test("DAL_BBG000R7Z112_2020-12-21", 2, 0.25m);
            Test("MFGP_BBG00HFWVGN0_2020-12-21", 2, 0.04m);
            Test("XOM_BBG000GZQ728_2020-12-21", 2, 0.01m);
            Test("C_BBG000FY4S11_2020-12-21", 2, 0.7m);
            Test("DBX_BBG0018SLDN0_2021-02-19", 2, 0.1m);
            Test("BLUE_BBG000QGWY50_2021-03-04", 2, 0.02m);


            Test("ATRA_BBG005Q3MQY4_2020-12-07", 0, 0.0m); // should not buy this: big grow from open
            Test("SIG_BBG000C4ZZ10_2020-12-09", 0, 0.0m); // not enough quotes
            Test("NTLA_BBG007KC7PB0_2020-12-14", 0, 0.0m); // no reasons to buy
            Test("FDX_BBG000BJF1Z8_2020-12-14", 0, 0.0m); // gap up, no reason to buy until prev day close price
            Test("QDEL_BBG000C6GN04_2021-01-19", 0, 0.0m); // gap up, do not buy

            Test("OXY_BBG000BQQ2S6_2020-12-21", 2, -0.7m); // big loss
            Test("FSLY_BBG004NLQHL0_2021-01-28", 1, -1.0m); // do not buy when falling
            Test("OIS_BBG000BDDN94_2021-01-06", 1, -0.1m); // big loss, price always low, do not buy!
            Test("DIS_BBG000BH4R78_2021-01-28", 1, -1.2m); // gap up
            Test("SPLK_BBG001C7TST4_2020-12-04", 1, -0.80m); // improve me: ignore first quote - it is too high(gap up)

            Test("SPCE_BBG00HTN2CQ3_2021-01-20", 1, -0.25m); // try to ignore first quote, but OK with lose
            Test("ET_BBG000BM2FL9_2020-12-15", 1, -0.04m); // do nothing. just lose
            Test("TER_BBG000BV4DR6_2021-01-28", 1, -0.40m); // happy to close with small loss


            Test("COTY_BBG000F395V1_2021-02-26", 1, -0.80m); // just no lack
            Test("SPCE_BBG00HTN2CQ3_2021-03-04", 2, -0.70m); // TODO: buy more!
            Test("PYPL_BBG0077VNXV6_2021-02-26", 1, -7.0m); // no lack, may be buy more
            Test("BIDU_BBG000QXWHD1_2021-02-26", 1, -6.0m); // no lack, may be buy more or minimize loss
            Test("VIPS_BBG002NLDLV8_2021-02-26", 1, -0.7m); // ChangeOpenToCurrent = 0
            Test("GSKY_BBG00KT2SCV8_2021-03-08", 1, -0.1m); // good candidate to buy more
            Test("DKNG_BBG00TCBG714_2021-03-05", 1, -0.80m); // no luck
            Test("AAL_BBG005P7Q881_2021-02-26", 1, -0.30m); // FIXME: why not closed with profit?!

            Test("SEDG_BBG0084BBZY6_2021-02-24", 1, -4.0m); // buy more or minimize loss
            Test("RCL_BBG000BB5792_2021-02-26", 1, -1.0m); // that a good example of why minimal loss is good against waiting profit
            Test("FSLY_BBG004NLQHL0_2021-02-24", 1, -0.5m); // loss OK
            Test("ICPT_BBG001J1QN87_2021-03-12", 1, -0.3m); // loss OK

            Test("", 1, 0.0m);
            _basePath = basePath;
        }

        private void SetUp()
        {
            // create settings
            var settings = new BaseBot.Settings(_options);
            settings.DumpQuotes = false;
            settings.FakeConnection = true;
            settings.RequestCandlesHistory = false;
            settings.SubscribeQuotes = false;

            if (settings.Strategies.Length == 0)
                settings.Strategies = "ImpulseStrategy";

            _strategy = settings.Strategies;

            // create bot
            _bot = new TradeBot(settings);
            _bot.StartAsync().Wait();

            _basePath = _options.Get<string>("CandlesPath") + "\\";
            if (!Directory.Exists(_basePath))
                throw new Exception("Directory does not exists: " + _basePath);

            _writer = new StreamWriter(_strategy + DateTime.Now.ToString("_yyyy_MM_dd_HH_mm_ss") + ".log", false);
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

        private void Test(string testName, int orders, decimal expectedProfit)
        {
            if (testName.Length == 0)
                return;

            if (_testNameFilter.Length > 0 && _testNameFilter != testName)
                return;

            Logger.Write("Test name: " + testName);
            _writer.WriteLine(testName);

            try
            {
                var fileName = _basePath + testName + ".csv";
                if (File.Exists(fileName))
                {
                    var candleList = TradeBot.ReadCandles(fileName);
                    var res = _bot.TradeByHistory(candleList);

                    decimal profit = 0.0m;
                    if (res.lots > 0)
                        profit = Math.Round(res.totalProfit / res.lots, 2);

                    bool passed = (res.totalOrders == orders && profit >= expectedProfit);
                    if (passed)
                    {
                        _writer.WriteLine("PASSED");
                        _writer.WriteLine("Orders: " + res.totalOrders);
                        _writer.WriteLine("Lots: " + res.lots);
                        _writer.WriteLine("Profit: " + profit);
                        _writer.WriteLine("TotalProfit: " + res.totalProfit);
                    }
                    else
                    {
                        _writer.WriteLine("FAILED");
                        _writer.WriteLine("Orders: {0}[{1}]", res.totalOrders, orders);
                        _writer.WriteLine("Lots: " + res.lots);
                        _writer.WriteLine("Profit: {0}[{1}]", profit, expectedProfit);
                        _writer.WriteLine("TotalProfit: {0}", res.totalProfit);
                    }

                    _totalOrders += res.totalOrders;
                    _totalProfit += res.totalProfit;
                }
                else
                {
                    _writer.WriteLine("FAILED");
                    _writer.WriteLine("File name {0} not found", fileName);
                }
            }
            catch (Exception e)
            {
                _writer.WriteLine("Exception: " + e.Message);
            }

            _writer.WriteLine("");
        }

        private void TestMorningOpenStrategy()
        {
            _testNameFilter = "";

            RunPositiveTestsMorningOpenStrategy();
            RunNegativeTestsMorningOpenStrategy();

            //Test("AMCX_BBG000H01H92_2021-01-28", 1, 0.0m); // improve me: open gap down and go down more
            //Test("SNAP_BBG00441QMJ7_2021-01-28", 1, 0.0m); // improve me: do not makes sense when big gap down but price is close to open price
            //Test("M_BBG000C46HM9_2021-01-28", 1, 0.0m); // improve me: per stat - it is more quotes until buy gap down
            //Test("INTC_BBG000C0G1D1_2021-01-22", 1, 0.0m); // TODO: FIXME! stat calculated wrong!
            //Test("PBF_BBG002832GV8_2021-01-28", 1, 0.0m); // improve me: do not by when price close to open and gap down
            //Test("SWBI_BBG000BM0QL7_2020-12-04", 1, 0.0m);


            Test("", 1, 0.0m);
            Test("", 1, 0.0m);

            //// all data test
            //var stat = _bot.TradeByHistory(_options.Get<string>("CandlesPath"), _options.Get<string>("OutputFolder"));

            //// log results
            //foreach (var it in stat)
            //{
            //    _writer.WriteLine(it.Key); // test name
            //    _writer.WriteLine(it.Value.totalProfit >= 0 ? "PASSED" : "FAILED");
            //    _writer.WriteLine("TotalOrders: " + it.Value.totalOrders);
            //    _writer.WriteLine("TotalProfit: " + it.Value.totalProfit);

            //    _totalOrders += it.Value.totalOrders;
            //    _totalProfit += it.Value.totalProfit;

            //    _writer.WriteLine("");
            //}
        }

        private void TestImpulseStrategy()
        {
            //_testNameFilter = "RDFN_BBG001Q7HP63_2021-01-07";

            RunPositiveTestsImpulseStrategy();
            RunNegativeTestsImpulseStrategy();

            // bad
            //Test("UPWK_BBG00FBJ6390_2021-01-27", 1, 0m);
            //Test("SPR_BBG000PRJ2Z9_2021-01-28", 1, 0m);
            //Test("GSKY_BBG00KT2SCV8_2021-01-11", 1, 0m); // not enough quotes.
            //Test("ZYXI_BBG000BJBXZ2_2021-01-12", 1, 0m); // so-so trend, small amount of quotes increased price
            //Test("ZYXI_BBG000BJBXZ2_2021-01-27", 1, 0m); // why did not closed early?! price does not grow!
            //Test("ICE_BBG000C1FB75_2021-01-12", 1, 0m); // improve closing or do not by
            //Test("TTM_BBG000PVGDH9_2021-01-15", 1, 0m); // not enough quotes. truncate trend if starting quotes do not matter

            // TODO
            //Test("SIG_BBG000C4ZZ10_2021-02-04", 1, 0m); // bad trend, do not buy me!!!

            //Test("MYGN_BBG000D9H9F1_2021-02-03", 1, -0.7m); // too late buy
            //Test("BBBY_BBG000CSY9H9_2021-01-13", 1, 0m); // improve me: swing SL -> 1 order
            //Test("ZYXI_BBG000BJBXZ2_2021-01-14", 1, 0m); // big grow with falls
            //Test("MAC_BBG000BL9C59_2021-01-26", 1, 0m); // short squeeze
            //Test("PBF_BBG002832GV8_2021-01-28", 1, 0m); // big losses. do not take into account max as day open
            //Test("AAPL_BBG000B9XRY4_2021-01-20", 1, 2.18m); //good grow.  small fall, but candles is not so big
            //Test("FSLY_BBG004NLQHL0_2021-01-22", 1, 0m); // do not by me...
            //Test("CPRI_BBG0029SNR63_2021-02-04", 1, 0m); // exit on TP when price is grow
            //Test("NKTR_BBG000BHCYJ1_2021-02-04", 1, 0m); // do not buy or improve moving SL
            //Test("PCAR_BBG000BQVTF5_2021-02-04", 1, 0m); // do not buy if possible: grow 2%, fall 1%

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
            //Test("", 1, 0m);
            //Test("", 1, 0m);
        }
    }
}
