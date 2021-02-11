using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;

namespace TradingBot
{
    public class Quotes : IDisposable
    {
        public class Quote
        {
            public decimal Price { get; set; }
            public decimal Volume { get; set; }
            public DateTime Time { get; set; }

            public Quote(decimal price, decimal volume, DateTime time)
            {
                Price = price;
                Volume = volume;
                Time = time;
            }
        }

        public List<CandlePayload> Candles { get; set; }
        public decimal AvgCandleChange { get; set; } // average candle change for last 2 hours
        public decimal DayMax { get; set; } // day maximum
        public decimal DayMin { get; set; } = decimal.MaxValue; // day minimum
        public List<int> RawPosStart { get; set; } // n-th element is a start pos in Raw of correspondent candle
        public List<Quote> Raw { get; set; }
        public QuoteLogger QuoteLogger { get; set; }

        public Quotes(string figi, string ticker, bool dumpQuotes)
        {
            Candles = new List<CandlePayload>();
            Raw = new List<Quote>();
            RawPosStart = new List<int>();
            QuoteLogger = new QuoteLogger(figi, ticker, dumpQuotes);
        }

        public virtual void Dispose()
        {
            QuoteLogger?.Dispose();
        }

        // number of quotes at the last specified interval
        public int GetNumberOfQuotes(int minutes = 1)
        {
            var threshold = DateTime.Now.AddMinutes(-1 * minutes);

            int count = 0;
            while (Raw.Count - count > 0 && Raw[Raw.Count - 1 - count].Time >= threshold)
                ++count;

            return count;
        }

        // Return average change of candles for the last 2 hours
        public decimal GetAvgCandleChange()
        {
            int count = 0;
            decimal summ = 0;
            for (int i = Math.Max(0, Candles.Count - 1 - 24); i < Candles.Count - 1; ++i)
            {
                if (Candles[i].Volume > 50)
                {
                    var change = Math.Abs(Helpers.GetChangeInPercent(Candles[i]));
                    if (change > 0.0m)
                    {
                        ++count;
                        summ += change;
                    }
                }
            }

            if (count > 0)
                return summ / count;

            return 0;
        }

        // Check if last quote was a spike
        public bool IsSpike()
        {
            decimal temp;
            return IsSpike(out temp);
        }
        public bool IsSpike(out decimal change)
        {
            // spike is a quote that diff against previous 3 quotes more than 3%
            change = 0;
            if (Raw.Count > 2 && Raw[Raw.Count - 1].Volume > 50)
            {
                var lastPrice = Raw[Raw.Count - 1].Price;

                // get average of previous 5 quotes
                decimal summ = 0;
                int count = 0;
                for (int i = Math.Max(0, Raw.Count - 4); i < Raw.Count - 1; ++i)
                {
                    summ += Raw[i].Price;
                    ++count;
                }

                if (count > 0)
                {
                    decimal avg = summ / count;
                    change = Helpers.GetChangeInPercent(avg, lastPrice);
                    return Math.Abs(change) > 3.0m;
                }
            }

            return false;
        }
    }
}
