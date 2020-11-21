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
            //BaseBot bot;

            /*
                    bot = new RocketBot(token, args[1]);
            /*/
            //bot = new Screener(token, args[1]);
            //*/

            //await bot.StartAsync();
            //while (true)
            //{
            //    bot.ShowStatus();
            //    System.Threading.Thread.Sleep(60000);
            //}

            var bot = new TradeBot(token, args[1]);
            await bot.StartAsync();

            var session_begin = DateTime.Today.AddDays(-1).AddHours(10).ToUniversalTime();
            var session_end = DateTime.Today.AddDays(-1).AddHours(24).ToUniversalTime();
            await bot.SaveHistory(session_begin, session_end);

            var res = bot.TradeByHistory("E:\\tinkoff\\TradingBot\\bin\\Debug\\netcoreapp3.1\\quote_history\\2020_11_20");
        }
    }
}
