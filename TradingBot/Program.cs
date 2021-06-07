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
                Logger.setOutputFolder(po.Get<string>("OutputFolder"));
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
                                try
                                {
                                    if (bot == null || (startTime.Day < DateTime.UtcNow.Day && DateTime.UtcNow.Hour >= 1))
                                    {
                                        startTime = DateTime.UtcNow;
                                        if (bot != null)
                                            await bot.DisposeAsync();

                                        bot = new Screener(settings);
                                        await bot.StartAsync();
                                    }

                                    bot.ShowStatus();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Write("Exception: " + ex.Message);
                                    bot = null;
                                }

                                System.Threading.Thread.Sleep(20000);
                            }
                        }
                    //break;

                    case "TradeBot":
                        {
                            TradeBot bot = null;
                            DateTime startTime = DateTime.UtcNow;
                            while (true)
                            {
                                try
                                {
                                    if (bot == null || (startTime.Day < DateTime.UtcNow.Day && DateTime.UtcNow.Hour >= 1))
                                    {
                                        startTime = DateTime.UtcNow;
                                        if (bot != null)
                                            await bot.DisposeAsync();

                                        bot = new TradeBot(settings);
                                        await bot.StartAsync();
                                    }

                                    bot.ShowStatus();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Write("Exception: " + ex.Message);
                                    bot = null;
                                }

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
                            //Utils.CorrectCandleID(po.Get<string>("CandlesPath"));
                            //Utils.ConvertQuotes(po.Get<string>("CandlesPath"));
                            Utils.SelectHistoryData(po.Get<string>("CandlesPath"), po.Get<string>("OutputFolder"));
                            //Utils.CorrectHistoryData(po.Get<string>("CandlesPath"), po.Get<string>("OutputFolder"));
                            //MorningOpenStatistic.CreateStatisticByHistoryData(po.Get<string>("CandlesPath"), po.Get<string>("OutputFolder"));
                            //TestMode(settings, po);
                        }
                        break;

                    case "CreateWatchList":
                        {
                            await Utils.CreateWatchList(settings, po);
                        }
                        break;

                    case "OperationReport":
                        {
                            settings.DumpQuotes = false;
                            settings.RequestCandlesHistory = false;
                            settings.SubscribeQuotes = false;
                            settings.FakeConnection = false;

                            BaseBot bot = new BaseBot(settings);
                            await bot.StartAsync();

                            await bot.OperationReport();
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
            string outputFolder = po.Get<string>("OutputFolder");
            settings.DumpQuotes = false;
            settings.SubscribeQuotes = false;
            settings.RequestCandlesHistory = false;
            settings.FakeConnection = true;

            if (!po.Get<bool>("CopyData"))
                outputFolder = null;

            var bot = new TradeBot(settings);
            await bot.StartAsync();
            bot.TradeByHistory(candlesPath, outputFolder, tickerFilter);
            await bot.DisposeAsync();
        }

        private static void TestMode(BaseBot.Settings settings, ProgramOptions po)
        {
            string candlesPath = po.Get<string>("CandlesPath");
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var outputFolder = po.Get<string>("OutputFolder");
                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                var fileStream = File.OpenRead(f.FullName);
                var streamReader = new StreamReader(fileStream);

                var line = streamReader.ReadLine(); // header
                var values = line.Split(";");
                if (values.Length < 7)
                    throw new Exception("Wrong file format with candles");

                DateTime prevTime = DateTime.Now;

                var outputName = outputFolder + "\\" + f.Name;
                var file = new StreamWriter(outputName, false);
                file.AutoFlush = true;
                file.WriteLine(line); // header

                // read first 10 candles to define if there a candle of previous day close
                List<string> buffer = new List<string>();
                while (buffer.Count < 10 && (line = streamReader.ReadLine()) != null)
                    buffer.Add(line);

                int candleID = 0;

                // check for prev day close candle
                if (buffer.Count > 0)
                {
                    values = buffer[0].Split(";");
                    DateTime firstLine = DateTime.Parse(values[1]);

                    int startPos = 0;
                    for (int i = 1; i < buffer.Count; ++i)
                    {
                        values = buffer[i].Split(";");
                        DateTime current = DateTime.Parse(values[1]);
                        if (firstLine > current)
                        {
                            startPos = i - 1;
                            break;
                        }
                    }

                    // write lines starting from previous day close if exists
                    for (int i = startPos; i < buffer.Count; ++i)
                    {
                        line = candleID.ToString() + buffer[i].Substring(buffer[i].IndexOf(";"));
                        file.WriteLine(line);
                        ++candleID;
                    }

                    values = buffer[buffer.Count - 1].Split(";");
                    prevTime = DateTime.Parse(values[1]);
                }

                while ((line = streamReader.ReadLine()) != null)
                {
                    values = line.Split(";");
                    var currentTime = DateTime.Parse(values[1]);

                    if (currentTime.AddMinutes(5) < prevTime)
                        break; // finished

                    prevTime = currentTime;

                    line = candleID.ToString() + line.Substring(line.IndexOf(";"));
                    file.WriteLine(line);
                    ++candleID;
                }

                file.Close();
                streamReader.Close();
                fileStream.Close();
            }
        }
    }
}
