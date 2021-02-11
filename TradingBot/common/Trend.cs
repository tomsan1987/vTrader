using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    public class Trend
    {
        public int StartPos { get; set; }
        public int EndPos { get; set; }
        public decimal Max { get; set; } // max value
        public decimal MaxFall { get; set; } // max negative deviation from trend line
        public decimal SD { get; set; } // standard deviation
        public decimal A { get; set; }
        public decimal B { get; set; }
    }
}
