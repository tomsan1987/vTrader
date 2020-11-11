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
    public class Base_bot : IAsyncDisposable
    {
        //private static readonly Random Random = new Random();
        protected readonly Context _context;
        protected string _accountId;
        protected List<MarketInstrument> _instruments;
        protected IList<string> _watch_list;
        protected Dictionary<string, string> _figi_to_ticker = new Dictionary<string, string>();
        protected Dictionary<string, string> _ticker_to_figi = new Dictionary<string, string>();
        protected string _config_path;

        public Base_bot(Context context, string config_path)
        {
            _context = context;
            _config_path = config_path;
            Init();
        }

        public void Init()
        {
            var config_json = JObject.Parse(File.ReadAllText(_config_path));
            _watch_list = ((JArray)config_json["watch-list"]).ToObject<IList<string>>();

            // get account ID
            var accounts = _context.AccountsAsync();
            accounts.Wait();
            foreach (var acc in accounts.Result)
            {
                _accountId = acc.BrokerAccountId;
            }

            var stocks = _context.MarketStocksAsync().Result;
            _instruments = stocks.Instruments;

            foreach (var ticker in _watch_list)
            {
                var idx = _instruments.FindIndex(x => x.Ticker == ticker);
                if (idx != -1)
                {
                    _figi_to_ticker.Add(_instruments[idx].Figi, _instruments[idx].Ticker);
                    _ticker_to_figi.Add(_instruments[idx].Ticker, _instruments[idx].Figi);
                }
            }
        }
        public async ValueTask DisposeAsync()
        {
        }
    }
}