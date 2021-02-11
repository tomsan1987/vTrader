using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using System.Globalization;

namespace TradingBot{
    class Helpers
    {
        static public decimal RoundPrice(decimal price, decimal minIncrement)
        {
            int units = (int)(price / minIncrement);
            return units * minIncrement;
        }

        static public decimal GetChangeInPercent(CandlePayload candle)
        {
            return GetChangeInPercent(candle.Open, candle.Close);
        }

        static public decimal GetChangeInPercent(decimal open, decimal close)
        {
            if (open <= close)
            {
                return Math.Round((close / open - 1) * 100, 2);
            }

            return -Math.Round((1 - close / open) * 100, 2);
        }

        static public bool IsRed(CandlePayload candle)
        {
            return candle.Open > candle.Close;
        }

        // get average and standard deviation
        static public void GetMS(List<Quotes.Quote> raw, int start, int end, out decimal M, out decimal S)
        {
            M = 0m;
            S = 0m;

            for (int i = start; i < end; ++i)
                M += raw[i].Price;

            M /= (end - start);

            for (int i = start; i < end; ++i)
                S += (raw[i].Price - M) * (raw[i].Price - M);

            S /= (end - start) /** 4*/;
            S = Math.Round((decimal)Math.Sqrt((double)S), 3);
            M = Math.Round(M, 2);
        }

        static public void Approximate(List<Quotes.Quote> raw, int start, int end, decimal step, out decimal a, out decimal b, out decimal max, out decimal maxFall, out decimal sd)
        {
            max = 0;
            maxFall = 0;
            sd = 0;

            decimal sumx = 0;
            decimal sumy = 0;
            decimal sumx2 = 0;
            decimal sumxy = 0;
            decimal high = 0;
            decimal low = decimal.MaxValue;
            for (int i = start; i < end; ++i)
            {
                var arg = (i - start) * step;
                sumx += arg;
                sumy += raw[i].Price;
                sumx2 += arg * arg;
                sumxy += arg * raw[i].Price;

                low = Math.Min(low, raw[i].Price);
                if (raw[i].Price > high || high == 0)
                {
                    high = raw[i].Price;
                    low = raw[i].Price;
                }

                maxFall = Math.Max(maxFall, GetChangeInPercent(low, high));
            }

            int n = end - start;
            a = (n * sumxy - (sumx * sumy)) / (n * sumx2 - sumx * sumx);
            b = (sumy - a * sumx) / n;

            for (int i = start; i < end; ++i)
            {
                var arg = (i - start) * step;
                var price = a * arg + b;
                //maxDeviation = Math.Min(maxDeviation, GetChangeInPercent(price, raw[i].Price));
                max = Math.Max(max, raw[i].Price);
                sd += (raw[i].Price - price) * (raw[i].Price - price);
            }

            sd /= (end - start);
            sd = Math.Round((decimal)Math.Sqrt((double)sd), 5);
            sd = GetChangeInPercent(raw[end - 1].Price, raw[end - 1].Price + sd);

            maxFall = Math.Abs(maxFall);
            a = Math.Round(a, 5);
            b = Math.Round(b, 5);
        }

        static public decimal Parse(string s)
        {
            s = s.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
            return decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        static public string CandleDesc(int id, CandlePayload c)
        {
            return String.Format("Candle: ID:{0}, Time: {1}, Close: {2}", id, c.Time.ToShortTimeString(), c.Close);
        }
    }
}
