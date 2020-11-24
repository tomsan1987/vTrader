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
            var token = (await File.ReadAllTextAsync(args[0])).Trim();

            // Screener
            //{
            //    var bot = new Screener(token, args[1]);
            //    await bot.StartAsync();
            //    while (true)
            //    {
            //        bot.ShowStatus();
            //        System.Threading.Thread.Sleep(60000);
            //    }
            //}

            // Rocket bot
            //{
            var bot = new RocketBot(token, args[1]);
            await bot.StartAsync();
            while (true)
            {
                bot.ShowStatus();
                System.Threading.Thread.Sleep(60000);
            }
            //}

            // Trade bot
            //{
            //var bot = new TradeBot(token, args[1]);
            //await bot.StartAsync();

            //var session_begin = new DateTime(2020, 11, 23).AddHours(10).ToUniversalTime();
            //var session_end = session_begin.AddHours(1).ToUniversalTime();

            //for (int i = 0; i < 10; ++i)
            //{
            //    await bot.SaveHistory(session_begin, session_end);
            //    session_begin = session_begin.AddHours(1);
            //    session_end = session_end.AddHours(1);
            //}

            //    var res = bot.TradeByHistory("E:\\tinkoff\\TradingBot\\bin\\Debug\\netcoreapp3.1\\quote_history\\2020_11_17", "");
            //}
        }
    }
}
