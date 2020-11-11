using System.IO;
using System.Threading.Tasks;

namespace TradingBot
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var token = (await File.ReadAllTextAsync(args[0])).Trim();
            await using var bot = new SandboxBot(token, args[1]);
            await bot.StartAsync();
        }
    }
}
