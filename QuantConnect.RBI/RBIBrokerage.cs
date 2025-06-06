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

using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuickFix.FIX42;
using QuickFix.Fields;
using QuantConnect.Brokerages.Fix;

namespace QuantConnect.Brokerages.RBI
{
    [BrokerageFactory(typeof(RBIBrokerageFactory))]
    public class RBIBrokerage : FixBrokerage
    {
        protected override string DataDictionaryFilePath => "RBI-FIX42.xml";

        public RBIBrokerage(FixConfiguration config,
            IOrderProvider orderProvider,
            IAlgorithm algorithm,
            LiveNodePacket job,
            ISecurityProvider securityProvider) : base(config, orderProvider, algorithm, job, "RBI")
        {
            var mapFileProvider = Composer.Instance.GetPart<IMapFileProvider>();
            var symbolMapper = new RBISymbolMapper(mapFileProvider);

            InitializeFix(new FixOrderController(symbolMapper, config.Account, config.OnBehalfOfCompID));
            ValidateSubscription(297);
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            if (string.IsNullOrEmpty(order.Symbol.ISIN))
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, 0, $"Invalid ISIN, failed to place order {order.Id} for symbol {order.Symbol}"));
                return false;
            }
            return base.PlaceOrder(order);
        }

        /// <summary>
        ///  Execution report receiver
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="report">Execution report</param>
        protected override void OnExecutionReport(object sender, ExecutionReport report)
        {
            var orderStatus = Fix.Utility.ConvertOrderStatus(report);

            string ordId;
            if (orderStatus == OrderStatus.UpdateSubmitted)
            {
                // order amendmends
                ordId = report.OrigClOrdID.getValue();
            }
            else if (orderStatus == OrderStatus.Canceled && report.IsSetField(OrigClOrdID.TAG))
            {
                // our cancelations use tag 41, unsolicited cancellation use ClOrdID
                ordId = report.OrigClOrdID.getValue();
            }
            else
            {
                ordId = report.ClOrdID.getValue();
            }
            OnExecutionReport(ordId, report);
        }
    }
}
