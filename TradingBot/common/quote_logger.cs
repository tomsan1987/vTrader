using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;

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
            _file = new StreamWriter(ticker + ".csv", false);
            _file.AutoFlush = true;
            _file.WriteLine("Time;Quote;Volume");
        }

        public void onQuoteReceived(CandleResponse res)
        {
            if (res.Payload.Figi == _figi)
            {
                var str = String.Format("{0};{1};{2}", res.Time.ToString(), res.Payload.Close, res.Payload.Volume);
                _file.WriteLine(str);
            }
        }
    }
}
