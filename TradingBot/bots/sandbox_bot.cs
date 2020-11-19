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
    public class Sandbox_bot : IAsyncDisposable
    {
        //private static readonly Random Random = new Random();
        private readonly Context _context;
        private string _accountId;
        private bool _work = true;
        private List<MarketInstrument> _instruments;
        private IList<string> _watch_list;
        private Dictionary<string, string> _figi_to_ticker = new Dictionary<string, string>();
        private Dictionary<string, string> _ticker_to_figi = new Dictionary<string, string>();
        private Dictionary<string, CandlePayload> _quotes = new Dictionary<string, CandlePayload>();
        private string _config_path;

        public Sandbox_bot(string token, string config_path)
        {
            var connection = ConnectionFactory.GetConnection(token);
            _context = connection.Context;
            _config_path = config_path;
        }

        public async Task StartAsync()
        {
            var config_json = JObject.Parse(File.ReadAllText(_config_path));
            _watch_list = ((JArray)config_json["watch-list"]).ToObject<IList<string>>();

            // register new sandbox account
            var sandboxAccount = await _context.AccountsAsync();
            foreach (var acc in sandboxAccount)
            {
                _accountId = acc.BrokerAccountId;
            }
            //_accountId = sandboxAccount.BrokerAccountId;

            var stocks = await _context.MarketStocksAsync();
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

            //// set balance
            //foreach (var currency in new[] {Currency.Rub, Currency.Usd, Currency.Eur})
            //    await _context.SetCurrencyBalanceAsync(currency, Random.Next(1, 10) * 100_000,
            //        sandboxAccount.BrokerAccountId);

            await CheckBalanceAsync();

            Query5MinTop();

            //RequestCandles();
            //SubscribeCandles();

            // select TESLA
            //var instrumentList = await _context.MarketStocksAsync();
            //var randomInstrumentIndex = Random.Next(instrumentList.Total);
            //var randomInstrument = instrumentList.Instruments.Find(x => x.Ticker == "TSLA");
            //Console.WriteLine($"Selected Instrument:\n{randomInstrument.ToString().Replace(", ", "\n")}");
            //Console.WriteLine();

            //// get candles
            //var now = DateTime.Now;
            //var candleList = await _context.MarketCandlesAsync(randomInstrument.Figi, now.AddMinutes(-5), now, CandleInterval.Minute);
            //foreach (var candle in candleList.Candles)
            //{
            //    Console.WriteLine(candle);
            //}
            //Console.WriteLine();

            // subscribe to candles
            //_context.StreamingEventReceived += OnStreamingEventReceived;
            //await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(randomInstrument.Figi, CandleInterval.Minute));

            //Console.WriteLine("Buy 1 lot");
            //await _context.PlaceMarketOrderAsync(new MarketOrder(randomInstrument.Figi, 1, OperationType.Buy,
            //    _accountId));

            //await CheckBalanceAsync();
            await Task.Delay(1000);


            while (_work)
            {
                //Task.Delay(1000);
            }

            //await _context.SendStreamingRequestAsync(StreamingRequest.UnsubscribeCandle(randomInstrument.Figi, CandleInterval.Minute));

            //Console.WriteLine("Sell 1 lot");
            //await _context.PlaceMarketOrderAsync(new MarketOrder(randomInstrument.Figi, 1, OperationType.Sell,
            //    _accountId));

            await CheckBalanceAsync();
        }

        private async Task CheckBalanceAsync()
        {
            var portfolio = await _context.PortfolioCurrenciesAsync(_accountId);
            Console.WriteLine("Balance");
            foreach (var currency in portfolio.Currencies) Console.WriteLine($"{currency.Balance} {currency.Currency}");

            Console.WriteLine();
        }

        public async ValueTask DisposeAsync()
        {

        }

        public async void Query5MinTop()
        {
            int idx = 0;
            for (int i = 0; i < _watch_list.Count; ++i)
            {
                var ticker = _watch_list[i];
                var figi = _ticker_to_figi[ticker];

                bool ok = false;
                while (!ok)
                {
                    try
                    {
                        ++idx;
                        var now = DateTime.Now;
                        var candleList = await _context.MarketCandlesAsync(figi, now.AddMinutes(-20), now, CandleInterval.FiveMinutes);
                        //if (candleList.Candles.Count > 3)
                        {
                            //decimal delta = candleList.Candles[candleList.Candles.Count - 1].Close / candleList.Candles[0].Close;
                            //decimal diff = candleList.Candles[candleList.Candles.Count - 1].Close - candleList.Candles[0].Close;
                            //if (delta >= (decimal)1.01)
                            //{
                            //    // check that we are in trend
                            //    int in_trend = 2;
                            //    for (int j = 1; j < candleList.Candles.Count - 1; ++j)
                            //    {
                            //        var expected = candleList.Candles[0].Close + diff / candleList.Candles.Count;
                            //        if (candleList.Candles[j].High <= expected * (decimal)1.001 && candleList.Candles[j].Low >= expected * (decimal)0.999)
                            //            ++in_trend;
                            //    }

                            //    if (in_trend / candleList.Candles.Count >= 0.8)
                            //        Console.WriteLine("{0}: {1} ->> {2}", ticker, candleList.Candles[0].Close, candleList.Candles[candleList.Candles.Count - 1].Close);
                            //}

                            //decimal delta = candleList.Candles[candleList.Candles.Count - 1].Close / candleList.Candles[0].Close;
                            //decimal diff = candleList.Candles[candleList.Candles.Count - 1].Close - candleList.Candles[0].Close;
                            //if (delta >= (decimal)1.002)
                            {
                                // check that we are in trend
                                int in_trend = 0;
                                for (int j = candleList.Candles.Count - 3; j < candleList.Candles.Count; ++j)
                                {
                                    decimal delta = candleList.Candles[j].Close / candleList.Candles[j].Open;
                                    if (delta >= (decimal)1.002)
                                        ++in_trend;
                                }

                                if (in_trend >= 3)
                                    Console.WriteLine("{0}: {1} ->> {2}", ticker, candleList.Candles[0].Close, candleList.Candles[candleList.Candles.Count - 1].Close);
                            }
                        }

                        ok = true;
                    }
                    catch (OpenApiException)
                    {
                        Console.WriteLine("Context: waiting after {0} queries....", idx);
                        ok = false;
                        idx = 0;
                        await Task.Delay(30000); // sleep for a while
                    }
                }
            }
        }

        public async void RequestCandles()
        {
            Console.WriteLine("Start query candles...");

            int processed = 0;
            for (int i = 0; i < _watch_list.Count; ++i)
            {
                var ticker = _watch_list[i];
                var figi = _ticker_to_figi[ticker];
                if (figi.Length > 0)
                {
                    bool ok = false;
                    while (!ok)
                    {
                        try
                        {
                            var now = DateTime.Now;
                            var candleList = await _context.MarketCandlesAsync(figi, now.AddMinutes(-15), now, CandleInterval.FiveMinutes);
                            if (candleList.Candles.Count > 0)
                            {
                                //var message = instrument.ToString();
                                //message += ", Volume: ";
                                //message += (volume / candleList.Candles.Count);

                                //  Console.WriteLine(message);
                            }

                            ok = true;
                        }
                        catch (OpenApiException)
                        {
                            Console.WriteLine("Context: waiting after {0} queries....", processed);
                            ok = false;
                            await Task.Delay(60000); // sleep for a while
                        }
                    }

                    ++processed;
                }
            }

            Console.WriteLine("End of query candles...");
        }

        public async void SubscribeCandles()
        {
            Console.WriteLine("Start subscribing candles...");

            // subscribe to candles
            _context.StreamingEventReceived += OnStreamingEventReceived;

            int processed = 0;
            for (int i = 0; i < _watch_list.Count; ++i)
            {
                var ticker = _watch_list[i];
                var figi = _ticker_to_figi[ticker];
                if (figi.Length > 0)
                {
                    bool ok = false;
                    while (!ok)
                    {
                        try
                        {
                            await _context.SendStreamingRequestAsync(StreamingRequest.SubscribeCandle(figi, CandleInterval.Minute));
                            ok = true;
                        }
                        catch (OpenApiException)
                        {
                            Console.WriteLine("Context: waiting after {0} queries....", processed);
                            ok = false;
                            await Task.Delay(60000); // sleep for a while
                        }
                    }

                    ++processed;
                }
            }

            Console.WriteLine("End of subscribing candles...");
        }

        private void OnStreamingEventReceived(object s, StreamingEventReceivedEventArgs e)
        {
            if (e.Response.Event == "candle")
            {
                var cr = (CandleResponse)e.Response;

                var q = _quotes[cr.Payload.Figi];
                if (q.Time.Minute == cr.Payload.Time.Minute)
                {
                    // update current candle
                    q = cr.Payload;
                }
                else
                {
                    //q.Close = cr.Payload.Close;
                }
                Console.WriteLine("{0}:{1}", _figi_to_ticker[cr.Payload.Figi], cr.Payload.Close);
            }
            else
            {
                Console.WriteLine(e.Response);
                //_work = false;
            }
        }
    }
}