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

        public Quotes(string figi, string ticker, bool dumpQuotes)
        {
            Candles = new List<CandlePayload>();
            Raw = new List<Quote>();
            QuoteLogger = new QuoteLogger(figi, ticker, dumpQuotes);
        }

        // number of quotes at the last specified interval
        public int GetNumberOfQuotes(int minutes = 1)
        {
            var threshold = DateTime.Now.AddMinutes(-1 * minutes);

            int count = 0;
            while (Raw.Count - count > 0 && Raw[Raw.Count - count].Time >= threshold)
                ++count;

            return count;
        }
        
        // Return average change of candles for the last hour
        public decimal GetAvgCandleChange()
        {
            int count = 0;
            decimal summ = 0;
            for (int i = Math.Max(0, Candles.Count - 1 - 12); i < Candles.Count - 1; ++i)
            {
                if (Candles[i].Volume > 50)
                {
                    ++count;
                    summ += Math.Abs(Helpers.GetChangeInPercent(Candles[i]));
                }
            }

            if (count > 0)
                return summ / count;

            return 0;
        }

        // Check if last quote was a spike
        public bool IsSpike()
        {
            // spike is a quote that diff against previous 3 quotes more than 0.5%
            if (Raw.Count > 1 && Raw[Raw.Count - 1].Volume > 10)
            {
                var lastPrice = Raw[Raw.Count - 1].Price;

                // get average of previous 3 quotes
                decimal summ = 0;
                int count = 0;
                for (int i = Math.Max(0, Raw.Count - 3); i < Raw.Count - 1; ++i)
                {
                    summ += Raw[i].Price;
                    ++count;
                }

                if (count > 0)
                {
                    decimal avg = summ / count;
                    var res = Math.Abs(Helpers.GetChangeInPercent(avg, lastPrice));
                    return res > (decimal)3.0;
                }
            }

            return false;
        }
    }
}
