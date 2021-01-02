using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;

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
                                    bot = new Screener(settings);
                                    await bot.StartAsync();
                                }

                                bot.ShowStatus();
                                System.Threading.Thread.Sleep(20000);
                            }
                        }
                        break;

                    case "TradeBot":
                        {
                            var bot = new TradeBot(settings);
                            await bot.StartAsync();
                            while (true)
                            {
                                bot.ShowStatus();
                                System.Threading.Thread.Sleep(20000);
                            }
                        }
                        break;

                    case "TestTradeBot":
                        {
                            await TestTradeBot(settings, po);
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

                        start = lastStr.IndexOf("Total/Pos/Neg ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf('.', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 14, end - (start + 14));
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
                            end = lastStr.IndexOf('.', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.totalProfit += decimal.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("Volume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf('.', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.volume += decimal.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("MaxVolume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf('.', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 11, end - (start + 11));
                                stat.maxVolume = Math.Max(stat.maxVolume, decimal.Parse(str));
                            }
                        }
                        else
                            break; // error parse


                        start = lastStr.IndexOf("Commission: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf('.', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 12, end - (start + 12));
                                stat.comission += decimal.Parse(str);
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
            // convert csv json quotes to simple csv data
            string candlesPath = po.Get<string>("CandlesPath");
            var list = TradeBot.ReadCandles(candlesPath);

            var fileName = Path.GetFileNameWithoutExtension(candlesPath);
            var dir = Path.GetDirectoryName(candlesPath);

            var outputName = dir + "\\" + fileName + "_simple.csv";
            var file = new StreamWriter(outputName, false);
            file.AutoFlush = true;

            List<Quotes.Quote> raw = new List<Quotes.Quote>();
            List<Trend> trends = new List<Trend>();

            // write header
            file.WriteLine("#;Time;Price;Volume;A;B;Change");
            for (int i = 0; i < list.Count; ++i)
            {
                var candle = list[i];
                raw.Add(new Quotes.Quote(candle.Close, candle.Volume, candle.Time));


                decimal a = 0, b = 0, change = 0;
                if (raw.Count > 500)
                {
                    double duration = (raw[raw.Count - 1].Time - raw[raw.Count - 500].Time).TotalSeconds;
                    if (duration == 0)
                        duration = 600;
                    decimal step = (decimal)(duration / 500);
                    Helpers.Approximate(raw, raw.Count - 500, raw.Count, step, out a, out b, out change);
                    change = Helpers.GetChangeInPercent(candle);
                }

                string message = String.Format("{0};{1};{2};{3};{4};{5};{6}", i, candle.Time.ToShortTimeString(), candle.Close, candle.Volume, a, b, change);
                message.Replace('.', ',');
                file.WriteLine(message);
            }

            file.Close();
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
    }
}
