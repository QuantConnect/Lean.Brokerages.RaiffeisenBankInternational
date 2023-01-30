using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Utils
{
    public static class Utility
    {
        public static OrderType ConvertOrderType(char orderType)
        {
            switch (orderType)
            {
                case OrdType.MARKET:
                    return OrderType.Market;

                case OrdType.LIMIT:
                    return OrderType.Limit;

                case OrdType.STOP:
                    return OrderType.StopMarket;

                case OrdType.STOP_LIMIT:
                    return OrderType.StopLimit;
                
                case OrdType.MARKET_ON_CLOSE:
                    return OrderType.MarketOnClose;

                default:
                    throw new NotSupportedException($"Unsupported order type: {orderType}");
            }
        }
        
        public static OrderStatus ConvertOrderStatus(ExecutionReport execution)
        {
            var execType = execution.ExecType.getValue();
            if (execType == ExecType.ORDER_STATUS)
            {
                execType = execution.OrdStatus.getValue();
            }

            switch (execType)
            {
                case ExecType.NEW:
                    return OrderStatus.Submitted;

                case ExecType.CANCELLED:
                    return OrderStatus.Canceled;

                case ExecType.REPLACED:
                    return OrderStatus.UpdateSubmitted;

                case ExecType.PARTIAL_FILL:
                    return OrderStatus.PartiallyFilled;

                case ExecType.FILL:
                    return OrderStatus.Filled;

                case ExecType.PENDING_NEW:
                    return OrderStatus.New;

                default:
                    return OrderStatus.Invalid;
            }
        }
    }
}
