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
                            System.Threading.Thread.Sleep(60000);
                        }
                    }
                    break;

                case "RocketBot":
                    {
                        var bot = new RocketBot(settings);
                        await bot.StartAsync();
                        while (true)
                        {
                            bot.ShowStatus();
                            System.Threading.Thread.Sleep(60000);
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
                        var bot = new RocketBot(settings);
                        bot.CreateCandlesStatistic(po.Get<string>("CandlesPath"));
                    }
                    break;

                default: Console.WriteLine("TODO: Help"); break;
            }
        }

        private static async Task TestRocketBot(BaseBot.Settings settings, ProgramOptions po)
        {
            string candlesPath = po.Get<string>("CandlesPath");
            string tickerFilter = po.Get<string>("TickerFilter");

            var bot = new RocketBot(settings);
            await bot.StartAsync();
            bot.TradeByHistory(candlesPath, tickerFilter);
            await bot.DisposeAsync();
        }
    }
}
