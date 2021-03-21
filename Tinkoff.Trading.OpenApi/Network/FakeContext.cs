using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Tinkoff.Trading.OpenApi.Models;

namespace Tinkoff.Trading.OpenApi.Network
{
    public class FakeContext : Context
    {
        public FakeContext(IConnection<FakeContext> connection) : base(connection)
        {
        }

        public override async Task<List<Order>> OrdersAsync(string brokerAccountId = null)
        {
            await Task.Yield();
            return new List<Order>();
        }

        public override async Task<PlacedLimitOrder> PlaceLimitOrderAsync(LimitOrder limitOrder)
        {
            await Task.Yield();
            return new PlacedLimitOrder(Guid.NewGuid().ToString().Substring(0, 8), limitOrder.Operation, OrderStatus.New, "", limitOrder.Lots, 0, new MoneyAmount(Currency.Usd, 1));
        }

        public override async Task CancelOrderAsync(string id, string brokerAccountId = null)
        {
            await Task.Yield();
        }
    }
}
