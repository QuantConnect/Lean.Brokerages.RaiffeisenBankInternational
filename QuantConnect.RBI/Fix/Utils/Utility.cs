/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Logging;
using QuantConnect.Orders;
using QuickFix.Fields;
using QuickFix.FIX42;
using TimeInForce = QuantConnect.Orders.TimeInForce;

namespace QuantConnect.Brokerages.RBI.Fix.Utils
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

                case ExecType.PENDING_CANCEL:
                    return OrderStatus.CancelPending;

                case ExecType.REJECTED:
                    return OrderStatus.Invalid;

                default:
                    Log.Error($"RBIBrokerage(): unsupported ExecType: {execType}");
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
