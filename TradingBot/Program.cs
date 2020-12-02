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
            var settings = new BaseBot.Settings();
            settings.Token = (await File.ReadAllTextAsync(args[0])).Trim();
            settings.ConfigPath = args[1];

            // Screener
            //{
            //    settings.DumpQuotes = true;
            //    settings.SubscribeQuotes = true;
            //    settings.RequestCandlesHistory = true;

            //    var bot = new Screener(settings);
            //    await bot.StartAsync();
            //    while (true)
            //    {
            //        bot.ShowStatus();
            //        System.Threading.Thread.Sleep(60000);
            //    }
            //}

            // Rocket bot
            //{
            //    settings.FakeConnection = false;
            //    settings.SubscribeQuotes = true;

            //    var bot = new RocketBot(settings);
            //    await bot.StartAsync();
            //    while (true)
            //    {
            //        bot.ShowStatus();
            //        System.Threading.Thread.Sleep(60000);
            //    }
            //    await bot.DisposeAsync();
            //}

            await TestRocketBot(settings);
        }

        private static async Task TestRocketBot(BaseBot.Settings settings)
        {
            settings.FakeConnection = true;
            settings.DumpQuotes = false;

            var bot = new RocketBot(settings);
            //bot.CreateCandlesStatistic("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-11-24_12_18");

            await bot.StartAsync();
            bot.TradeByHistory("E:\\tinkoff\\TestData\\RawQuotes\\5m\\quotes_2020-11-24_12_18", "");
            await bot.DisposeAsync();
        }

    }
}
