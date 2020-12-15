using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    public enum Status
    {
        Watching,           // watching quotes
        BuyPending,         // limited order created(Buy)
        BuyDone,            // limited order executed(we have stock)
        SellPending,        // limited order created(Sell)
        SellDone,           // limited order executed(we have no stock)
        ShutDown,           // cancel limited orders, sell lots if any
    }
}
