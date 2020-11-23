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

            //var session_begin = new DateTime(2020, 11, 16).AddHours(10).ToUniversalTime();
            //var session_end = session_begin.AddHours(14).ToUniversalTime();

            //for (int i = 0; i < 4; ++i)
            //{
            //    await bot.SaveHistory(session_begin, session_end);
            //    session_begin = session_begin.AddDays(1);
            //    session_end = session_end.AddDays(1);
            //}

            var res = bot.TradeByHistory("E:\\tinkoff\\TradingBot\\bin\\Debug\\netcoreapp3.1\\quote_history\\2020_11_17", "");
        }
    }
}
