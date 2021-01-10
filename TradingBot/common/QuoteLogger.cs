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
    //     Simple dumper quotes to text file.
    public class QuoteLogger
    {
        private readonly string _figi;
        private readonly string _ticker;
        private readonly StreamWriter _file;
        private int _quotesCount;

        public QuoteLogger(string figi, string ticker, bool dumpQuotes)
        {
            _figi = figi;
            _ticker = ticker;
            _quotesCount = 0;

            if (dumpQuotes)
            {
                string dirName = "quotes_" + DateTime.Now.ToString("yyyy-MM-dd");
                Directory.CreateDirectory(dirName);

                var fileName = dirName + "\\" + ticker + "_" + figi + DateTime.Now.ToString("_yyyy-MM-dd") + ".csv";
                bool exists = File.Exists(fileName);

                _file = new StreamWriter(fileName, true);
                _file.AutoFlush = true;

                if (!exists)
                    _file.WriteLine("#;Time;Open;Close;Low;High;Volume");
            }
        }

        public void onQuoteReceived(CandlePayload candle)
        {
            if (candle.Figi == _figi && _file != null)
            {
                string message = String.Format("{0};{1};{2};{3};{4};{5};{6}", _quotesCount++, candle.Time.ToShortTimeString(), candle.Open, candle.Close, candle.Low, candle.High, candle.Volume);
                message.Replace('.', ',');
                _file.WriteLine(message);
            }
        }
    }
}
