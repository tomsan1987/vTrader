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
            var connection = ConnectionFactory.GetConnection(token);

            /*
            // Rocket bot
            await using var bot = new Rocket_bot(connection.Context, args[1]);
            await bot.StartAsync();

            /*/

            // Screener
            await using var bot = new Screener(connection.Context, args[1]);
            await bot.StartAsync();

            //*/

            while (true)
                System.Threading.Thread.Sleep(50000);
        }
    }
}
