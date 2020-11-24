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
            _file = new StreamWriter("quotes\\" + ticker + ".csv", true);
            _file.AutoFlush = true;
            _file.WriteLine("Time;Quote;Volume");
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
