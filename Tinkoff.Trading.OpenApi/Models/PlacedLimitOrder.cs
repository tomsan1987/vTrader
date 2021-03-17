using Newtonsoft.Json;

namespace Tinkoff.Trading.OpenApi.Models
{
    public class PlacedLimitOrder
    {
        public string OrderId { get; set; }
        public OperationType Operation { get; }
        public OrderStatus Status { get; }
        public string RejectReason { get; }
        public int RequestedLots { get; set; }
        public int ExecutedLots { get; set; }
        public MoneyAmount Commission { get; }
        public string Figi { get; set; }
        public decimal Price { get; set; }

        [JsonConstructor]
        public PlacedLimitOrder(string orderId, OperationType operation, OrderStatus status, string rejectReason, int requestedLots, int executedLots, MoneyAmount commission)
        {
            OrderId = orderId;
            Operation = operation;
            Status = status;
            RejectReason = rejectReason;
            RequestedLots = requestedLots;
            ExecutedLots = executedLots;
            Commission = commission;
        }
    }
}
