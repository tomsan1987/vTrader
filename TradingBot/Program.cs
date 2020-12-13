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
            var po = new ProgramOptions(args);
            var settings = new BaseBot.Settings(po);
            Logger.Write(po.ToString());

            var mode = po.Get<string>("mode");
            switch (mode)
            {
                case "Screener":
                    {
                        var bot = new Screener(settings);
                        await bot.StartAsync();
                        while (true)
                        {
                            bot.ShowStatus();
                            System.Threading.Thread.Sleep(20000);
                        }
                    }
                    break;

                case "RocketBot":
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

                case "TestRocketBot":
                    {
                        await TestRocketBot(settings, po);
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

        private static async Task TestRocketBot(BaseBot.Settings settings, ProgramOptions po)
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
            string destinationFolder = new string("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-12-02");
            DirectoryInfo folder = new DirectoryInfo("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-12-02_18_04");
            foreach (FileInfo f in folder.GetFiles())
            {
                if (f.Extension == ".csv")
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
}
