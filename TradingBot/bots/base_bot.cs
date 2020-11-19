using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace TradingBot
{
    //
    // Summary:
    //     Base class for all trade bots.
    public class BaseBot : IAsyncDisposable
    {
        //private static readonly Random Random = new Random();
        protected readonly Context _context;
        protected string _accountId;
        protected List<MarketInstrument> _instruments;
        protected IList<string> _watchList;
        protected Dictionary<string, string> _figiToTicker = new Dictionary<string, string>();
        protected Dictionary<string, MarketInstrument> _figiToInstrument = new Dictionary<string, MarketInstrument>();
        protected Dictionary<string, string> _tickerToFigi = new Dictionary<string, string>();
        protected string _configPath;

        public BaseBot(Context context, string configPath)
        {
            _context = context;
            _configPath = configPath;
            Init();
        }

        public void Init()
        {
            var configJson = JObject.Parse(File.ReadAllText(_configPath));
            _watchList = ((JArray)configJson["watch-list"]).ToObject<IList<string>>();

            // get account ID
            var accounts = _context.AccountsAsync();
            accounts.Wait();
            foreach (var acc in accounts.Result)
            {
                _accountId = acc.BrokerAccountId;
            }

            var stocks = _context.MarketStocksAsync().Result;
            _instruments = stocks.Instruments;

            foreach (var ticker in _watchList)
            {
                var idx = _instruments.FindIndex(x => x.Ticker == ticker);
                if (idx != -1)
                {
                    _figiToTicker.Add(_instruments[idx].Figi, _instruments[idx].Ticker);
                    _tickerToFigi.Add(_instruments[idx].Ticker, _instruments[idx].Figi);
                    _figiToInstrument.Add(_instruments[idx].Figi, _instruments[idx]);
                }
                else
                {
                    throw new Exception("Unknown ticker: " + ticker);
                }
            }
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }

        protected decimal getMinIncrement(string figi)
        {
            return _figiToInstrument[figi].MinPriceIncrement;
        }
    }
}