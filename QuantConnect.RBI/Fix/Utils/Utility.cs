using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;
using QuickFix.Fields;
using QuickFix.FIX42;
using TimeInForce = QuantConnect.Orders.TimeInForce;

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
                
                //uncomment if it's correct
                // case OrdType.MARKET_IF_TOUCHED:
                //     return OrderType.LimitIfTouched;

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
        
        public static QuickFix.Fields.TimeInForce ConvertTimeInForce(TimeInForce timeInForce, OrderType orderType)
        {
            if (timeInForce == TimeInForce.GoodTilCanceled)
            {
                if (orderType == OrderType.StopMarket || orderType == OrderType.StopLimit)
                {
                    
                    return new QuickFix.Fields.TimeInForce(QuickFix.Fields.TimeInForce.DAY);
                }

                return new QuickFix.Fields.TimeInForce(QuickFix.Fields.TimeInForce.GOOD_TILL_CANCEL);
            }

            if (timeInForce == TimeInForce.Day)
            {
                return new QuickFix.Fields.TimeInForce(QuickFix.Fields.TimeInForce.DAY);
            }

            if (timeInForce == TimeInForce.GoodTilDate(DateTime.Now.AddYears(1)))
            {
                if (orderType == OrderType.StopMarket || orderType == OrderType.StopLimit)
                {
                    return new QuickFix.Fields.TimeInForce(QuickFix.Fields.TimeInForce.DAY);
                }

                return new QuickFix.Fields.TimeInForce(QuickFix.Fields.TimeInForce.GOOD_TILL_DATE);
            }

            throw new NotSupportedException($"Unsupported TimeInForce: {timeInForce.GetType().Name}");
        }
    }
}
