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
        protected Context _context;
        protected string _token;
        protected string _accountId;
        protected List<MarketInstrument> _instruments;
        protected IList<string> _watchList;
        protected Dictionary<string, string> _figiToTicker = new Dictionary<string, string>();
        protected Dictionary<string, MarketInstrument> _figiToInstrument = new Dictionary<string, MarketInstrument>();
        protected Dictionary<string, string> _tickerToFigi = new Dictionary<string, string>();
        protected string _configPath;

        public BaseBot(string token, string configPath)
        {
            _token = token;
            _configPath = configPath;
        }

        public virtual async Task StartAsync()
        {
            Connect();
            await Init();
        }

        public virtual void ShowStatus()
        {        
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }

        protected async Task Init()
        {
            var configJson = JObject.Parse(File.ReadAllText(_configPath));
            _watchList = ((JArray)configJson["watch-list"]).ToObject<IList<string>>();

            // get account ID
            var accounts = await _context.AccountsAsync();
            foreach (var acc in accounts)
            {
                _accountId = acc.BrokerAccountId;
            }

            var stocks = await _context.MarketStocksAsync();
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

        protected void Connect()
        {
            var connection = ConnectionFactory.GetConnection(_token);
            _context = connection.Context;
            Logger.Write("Connection created");
        }

        protected decimal getMinIncrement(string figi)
        {
            return _figiToInstrument[figi].MinPriceIncrement;
        }

        protected async Task SubscribeCandles()
        {
            Logger.Write("Start subscribing candles...");

            for (int i = 0; i < _watchList.Count; ++i)
            {
                var ticker = _watchList[i];
                var figi = _tickerToFigi[ticker];
                await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(figi, CandleInterval.FiveMinutes));
            }

            Logger.Write("End of subscribing candles...");
        }
    }
}