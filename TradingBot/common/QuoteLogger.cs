using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    //
    // Summary:
    //     Simple dunper quotes to text file.
    public class QuoteLogger
    {
        private readonly string _figi;
        private readonly string _ticker;
        private readonly StreamWriter _file;

        public QuoteLogger(string figi, string ticker)
        {
            _figi = figi;
            _ticker = ticker;

            string dirName = "quotes_" + DateTime.Now.ToString("yyyy-MM-dd_HH_mm");
            Directory.CreateDirectory(dirName);
            _file = new StreamWriter(dirName + "\\" + ticker + ".csv", true);
            _file.AutoFlush = true;
        }

        public void onQuoteReceived(CandlePayload candle)
        {
            if (candle.Figi == _figi)
            {
                _file.WriteLine(JsonConvert.SerializeObject(candle));
            }
        }
    }
}
