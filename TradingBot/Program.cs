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
            BaseBot bot;

            /*
                    bot = new RocketBot(token, args[1]);
            /*/
                    bot = new Screener(token, args[1]);
            //*/

            await bot.StartAsync();
            while (true)
            {
                bot.ShowStatus();
                System.Threading.Thread.Sleep(60000);
            }
        }
    }
}
