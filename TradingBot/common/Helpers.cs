using System;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;

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
    }
}
