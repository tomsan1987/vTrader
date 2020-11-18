using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot{
    class Helpers
    {
        static public decimal round_price(decimal price, decimal min_increment)
        {
            int units = (int)(price / min_increment);
            return units * min_increment;
        }
    }
}
