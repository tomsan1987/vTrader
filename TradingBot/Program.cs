using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json.Linq;

namespace TradingBot
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                var po = new ProgramOptions(args);
                var settings = new BaseBot.Settings(po);
                Logger.Write(po.ToString());

                var mode = po.Get<string>("mode");
                switch (mode)
                {
                    case "Screener":
                        {
                            Screener bot = null;
                            DateTime startTime = DateTime.UtcNow;
                            while (true)
                            {
                                if (bot == null || (startTime.Day < DateTime.UtcNow.Day && DateTime.UtcNow.Hour >= 1))
                                {
                                    startTime = DateTime.UtcNow;
                                    bot?.DisposeAsync();
                                    bot = new Screener(settings);
                                    await bot.StartAsync();
                                }

                                bot.ShowStatus();
                                System.Threading.Thread.Sleep(20000);
                            }
                        }
                        //break;

                    case "TradeBot":
                        {
                            await using var bot = new TradeBot(settings);
                            await bot.StartAsync();
                            while (true)
                            {
                                bot.ShowStatus();
                                System.Threading.Thread.Sleep(20000);
                            }
                        }
                        //break;

                    case "TestTradeBot":
                        {
                            await TestTradeBot(settings, po);
                        }
                        break;

                    case "RunTests":
                        {
                            var tester = new TradeBotTester(po);
                            tester.Run();
                        }
                        break;

                    case "AnalyzeLogs":
                        {
                            AnalyzeLogs(settings, po);
                        }
                        break;

                    case "CreateCandlesStatistic":
                        {
                            var bot = new TradeBot(settings);
                            bot.CreateCandlesStatistic(po.Get<string>("CandlesPath"));
                        }
                        break;

                    case "TestMode":
                        {
                            TestMode(settings, po);
                        }
                        break;

                    case "ConvertQuotes":
                        {
                            ConvertQuotes(settings, po);
                        }
                        break;

                    case "CreateWatchList":
                        {
                            await CreateWatchList(settings, po);
                        }
                        break;

                    default: Console.WriteLine("TODO: Help"); break;
                }
            }
            catch (Exception e)
            {
                Logger.Write(e.Message);
            }
        }

        private static async Task TestTradeBot(BaseBot.Settings settings, ProgramOptions po)
        {
            string candlesPath = po.Get<string>("CandlesPath");
            string tickerFilter = po.Get<string>("TickerFilter");

            var bot = new TradeBot(settings);
            await bot.StartAsync();
            bot.TradeByHistory(candlesPath, tickerFilter);
            await bot.DisposeAsync();
        }

        private static void AnalyzeLogs(BaseBot.Settings settings, ProgramOptions po)
        {
            bool ok = false;
            TradeStatistic stat = new TradeStatistic();

            string sourceFolder = System.IO.Directory.GetCurrentDirectory();
            if (po.HasValue("SourceFolder"))
                sourceFolder = po.Get<string>("SourceFolder");

            Console.WriteLine(sourceFolder);

            DirectoryInfo folder = new DirectoryInfo(sourceFolder);
            foreach (FileInfo f in folder.GetFiles("*.log"))
            {
                ok = false;
                var lastStr = String.Empty;
                var lines = File.ReadLines(f.FullName).GetEnumerator();
                while (lines.MoveNext())
                    lastStr = lines.Current;

                if (lastStr.Length > 0)
                {
                    //Trade statistic. Total/Pos/Neg 42/16/26. Profit: -6,46. Volume: 3120,24. MaxVolume: 1704,93.Commission: 3,117010
                    for (; ;)
                    {
                        int start = 0;
                        int end = 0;

                        start = lastStr.IndexOf("Total/Pos/Neg: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 15, end - (start + 15));
                                var array = str.Split("/");
                                if (array.Length == 3)
                                {
                                    stat.totalOrders += int.Parse(array[0]);
                                    stat.posOrders += int.Parse(array[1]);
                                    stat.negOrders += int.Parse(array[2]);
                                }
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("Profit: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.totalProfit += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("Volume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.volume += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("MaxVolume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 11, end - (start + 11));
                                stat.maxVolume = Math.Max(stat.maxVolume, Helpers.Parse(str));
                            }
                        }
                        else
                            break; // error parse


                        start = lastStr.IndexOf("Commission: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 12, end - (start + 12));
                                stat.comission += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        ok = true;
                        break;
                    }

                    if (!ok)
                        Logger.Write("Error getting stats for {0}. Line: {1}", f.FullName, lastStr);
                }
            }

            if (stat.totalOrders > 0)
                Logger.Write(stat.GetStringStat());
        }

        private static void TestMode(BaseBot.Settings settings, ProgramOptions po)
        {
            string candlesPath = po.Get<string>("CandlesPath");
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var fileStream = File.OpenRead(f.FullName);
                var streamReader = new StreamReader(fileStream);

                var line = streamReader.ReadLine(); // skip header
                var values = line.Split(";");
                if (values.Length < 7)
                    throw new Exception("Wrong file format with candles");

                int recSkip = 2;
                DateTime prevTime = DateTime.Now;

                var outputName = po.Get<string>("OutputFolder") + "\\" + f.Name;
                var file = new StreamWriter(outputName, false);
                file.AutoFlush = true;
                file.WriteLine(line);

                while ((line = streamReader.ReadLine()) != null)
                {
                    values = line.Split(";");
                    var currentTime = DateTime.Parse(values[1]);

                    if (recSkip > 0)
                    {
                        prevTime = DateTime.Parse(values[1]);
                        //file.WriteLine(line);
                        recSkip--;
                        continue;
                    }

                    if (currentTime >= prevTime)
                    {
                        prevTime = currentTime;
                        file.WriteLine(line);
                    }
                    else
                        break; // finished
                }

                file.Close();
                streamReader.Close();
                fileStream.Close();
            }
        }

        private static void ConvertQuotes(BaseBot.Settings settings, ProgramOptions po)
        {
            string candlesPath = po.Get<string>("CandlesPath");
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.json"))
            {
                // convert json quotes to simple csv data
                var list = TradeBot.ReadCandles(f.FullName);
                if (list.Count == 0)
                {
                    Logger.Write("No data found for " + f.FullName);
                    continue;
                }

                var figi = list[list.Count / 2].Figi;
                var date = list[list.Count / 2].Time.ToString("_yyyy-MM-dd");
                var fileName = Path.GetFileNameWithoutExtension(f.FullName) + "_" + figi + date;
                var dir = Path.GetDirectoryName(f.FullName);

                var outputName = dir + "\\" + fileName + ".csv";
                var file = new StreamWriter(outputName, false);
                file.AutoFlush = true;

                List<Quotes.Quote> raw = new List<Quotes.Quote>();
                List<Trend> trends = new List<Trend>();

                // write header
                file.WriteLine("#;Time;Open;Close;Low;High;Volume");
                for (int i = 0; i < list.Count; ++i)
                {
                    var candle = list[i];
                    raw.Add(new Quotes.Quote(candle.Close, candle.Volume, candle.Time));

                    string message = String.Format("{0};{1};{2};{3};{4};{5};{6}", i, candle.Time.ToShortTimeString(), candle.Open, candle.Close, candle.Low, candle.High, candle.Volume);
                    message.Replace('.', ',');
                    file.WriteLine(message);
                }

                file.Close();
            }
        }

        private async static Task CreateWatchList(BaseBot.Settings settings, ProgramOptions po)
        {
            var connection = ConnectionFactory.GetConnection(settings.Token);
            var context = connection.Context;

            string _accountId = "";
            var accounts = await context.AccountsAsync();
            foreach (var acc in accounts)
            {
                if (acc.BrokerAccountType == BrokerAccountType.Tinkoff)
                    _accountId = acc.BrokerAccountId;
            }

            var instruments = (await context.MarketStocksAsync()).Instruments;

            Action<Currency> store = c =>
            {
                List<string> list = new List<string>();
                foreach (var it in instruments)
                {
                    if (it.Currency == c)
                        list.Add(it.Ticker);
                }

                list.Sort();

                JArray array = new JArray();
                foreach (var ticker in list)
                    array.Add(ticker);

                JObject json = new JObject();
                json["watch-list"] = array;

                var file = new StreamWriter(c.ToString() + ".json", false);
                file.AutoFlush = true;
                file.Write(json.ToString());
                file.Close();
            };

            store(Currency.Rub);
            store(Currency.Usd);
        }
    }
}
