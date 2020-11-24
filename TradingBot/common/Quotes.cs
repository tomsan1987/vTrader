using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;

namespace TradingBot
{
    public class Quotes
    {
        public class Quote
        { 
            public decimal Price { get; set; }
            public decimal Volume { get; set; }
            public DateTime Time { get; set; }

            public Quote(decimal price, decimal volume)
            {
                Price = price;
                Volume = volume;
                Time = DateTime.Now;
            }
        }

        public List<CandlePayload> Candles { get; set; }
        public List<Quote> Raw { get; set; }
        public QuoteLogger QuoteLogger { get; set; }

        public Quotes(string figi, string ticker)
        {
            Candles = new List<CandlePayload>();
            Raw = new List<Quote>();
            QuoteLogger = new QuoteLogger(figi, ticker);
        }

        // number of quotes at the last specified interval
        public int getNumberOfQuotes(int minutes = 1)
        {
            var threshold = DateTime.Now.AddMinutes(-1 * minutes);

            int count = 0;
            while (Raw.Count - count > 0 && Raw[Raw.Count - count].Time >= threshold)
                ++count;

            return count;
        }
    }
}
