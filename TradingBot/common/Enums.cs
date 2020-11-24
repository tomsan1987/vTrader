using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    public enum Status
    {
        Watching,           // watching quotes
        BuyPending,         // limited order creaded(Buy)
        BuyDone,            // limited order executed(we have stock)
        SellPending,        // limited order created(Sell)
        SellDone,           // limited order executed(we have no stock)
        ShutDown,           // cancell limited orders, sell lots if any
    }
}
