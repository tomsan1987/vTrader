using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;


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
                            Utils.AnalyzeLogs(po.Get<string>("SourceFolder"));
                        }
                        break;

                    case "CreateCandlesStatistic":
                        {
                            Utils.CreateCandlesStatistic(po.Get<string>("CandlesPath"));
                        }
                        break;

                    case "TestMode":
                        {
                            Utils.CorrectCandleID(po.Get<string>("CandlesPath"));
                            //Utils.ConvertQuotes(po.Get<string>("CandlesPath"));
                            //TestMode(settings, po);
                        }
                        break;

                    case "CreateWatchList":
                        {
                            await Utils.CreateWatchList(settings, po);
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
    }
}
