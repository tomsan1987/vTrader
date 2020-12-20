using System;
using System.IO;
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
                            await TestRocketBot(settings, po);
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

                    default: Console.WriteLine("TODO: Help"); break;
                }
            }
            catch (Exception e)
            {
                Logger.Write(e.Message);
            }
        }

        private static async Task TestRocketBot(BaseBot.Settings settings, ProgramOptions po)
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
            string destinationFolder = new string("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-12-02");
            DirectoryInfo folder = new DirectoryInfo("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-12-02_18_04");
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var destinationPath = destinationFolder + "\\" + f.Name;
                if (File.Exists(destinationPath))
                {
                    // if file exist in sorce folder
                    var file = new StreamWriter(destinationPath, true);
                    file.AutoFlush = true;

                    var fileStream = File.OpenRead(f.FullName);
                    var streamReader = new StreamReader(fileStream);
                    String line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        file.WriteLine(line);
                    }

                    file.Close();
                    streamReader.Close();
                    fileStream.Close();
                }
                else
                {
                    // copy to source folder
                    File.Copy(f.FullName, destinationPath);
                }
            }
        }
    }
}
