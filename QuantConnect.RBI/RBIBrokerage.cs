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

using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Brokerages;
using QuantConnect.Orders.Fees;
using QuantConnect.RBI.Fix;
using QuantConnect.RBI.Fix.Core;
using QuantConnect.RBI.Fix.Core.Implementations;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuickFix;
using QuickFix.FIX42;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.RBI
{
    [BrokerageFactory(typeof(RBIBrokerageFactory))]
    public class RBIBrokerage : Brokerage
    {
        private readonly IFixBrokerageController _fixBrokerageController;
        private readonly FixInstance _fixInstance;
        private readonly RBISymbolMapper _symbolMapper;
        
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;
        private readonly IOrderProvider _orderProvider;
        private readonly IAlgorithm _algorithm;
        private readonly LiveNodePacket _job;

        public RBIBrokerage(
            FixConfiguration config,
            IOrderProvider orderProvider,
            IAlgorithm algorithm,
            LiveNodePacket job) : base("RBI")
        {
            _algorithm = algorithm;
            _job = job;
            _orderProvider = orderProvider;

            _symbolMapper = new RBISymbolMapper();

            _fixBrokerageController = new FixBrokerageController(_symbolMapper);

            _fixBrokerageController.ExecutionReport += OnExecutionReport;
            
            var fixProtocolDirector = new FixMessageHandler(config, _fixBrokerageController);
            _fixInstance = new FixInstance(fixProtocolDirector, config);
        }

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public override bool IsConnected => _fixInstance.IsConnected();

        #region Brokerage
        
        /// <summary>
        /// Gets all open orders on the account.
        /// NOTE: The order objects returned do not have QC order IDs.
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        public override List<Order> GetOpenOrders()
        {
            return _fixBrokerageController.GetOpenOrders();
        }
        
        /// <summary>
        /// Gets all holdings for the account
        /// </summary>
        /// <returns>The current holdings from the account</returns>
        public override List<Holding> GetAccountHoldings()
        {
            return GetAccountHoldings(_job.BrokerageData, _algorithm.Securities.Values);
        }
        
        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public override List<CashAmount> GetCashBalance()
        {
            return GetCashBalance(_job.BrokerageData, _algorithm.Portfolio.CashBook);
        }
        
        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            return _fixBrokerageController.PlaceOrder(order);
        }
        
        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Order order)
        {
            return _fixBrokerageController.UpdateOrder(order);
        }
        
        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        public override bool CancelOrder(Order order)
        {
            return _fixBrokerageController.CancelOrder(order);
        }
        
        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        public void Connect(FixConfiguration config)
        {
            _fixInstance.Initialize();
            var sessionId = new SessionID(config.FixVersionString, config.SenderCompId, config.TargetCompId);

            _fixInstance.OnLogon(sessionId);
        }

        public override void Connect()
        {
            _fixInstance.Initialize();

        }

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public override void Disconnect()
        {
            _fixInstance.Terminate();
        }
        
        #endregion

        private void OnExecutionReport(object sender, ExecutionReport report)
        {
            Log.Trace($"OnExecutionReport(): {sender} sent {report}");

            var orderStatus = Utility.ConvertOrderStatus(report);

            var ordId = orderStatus == OrderStatus.Canceled
                ? report.OrigClOrdID.getValue()
                : report.ClOrdID.getValue();

            var transactTime = report.TransactTime.getValue();

            //var order = _orderProvider.GetOrderByBrokerageId(ordId); change to this!!!
            var order = _orderProvider.GetOrders(o => true).FirstOrDefault();

            if (order == null)
            {
                Log.Trace($"No order with Id {ordId} was found");
                return;
            }

            var message = "RBI OnOrderEvent";

            if (report.IsSetText())
            {
                message += $" - {report.Text.getValue()}";
            }

            var orderEvent = new OrderEvent(order, transactTime, OrderFee.Zero, message)
            {
                Status = orderStatus
            };

            if (orderStatus == OrderStatus.Filled || orderStatus == OrderStatus.PartiallyFilled)
            {
                var filledQuantity = report.LastShares.getValue();
                var remainingQuantity = order.AbsoluteQuantity - report.CumQty.getValue();

                orderEvent.FillQuantity = filledQuantity * (order.Direction == OrderDirection.Buy ? 1 : -1);
                orderEvent.FillPrice = report.LastPx.getValue();

                if (remainingQuantity > 0)
                {
                    orderEvent.Message += " - " + remainingQuantity + " shares remaining";
                }
            }

            if (report.OrdStatus.getValue() == QuickFix.Fields.OrdStatus.DONE_FOR_DAY)
            { 
                var filledQuantity = report.LastShares.getValue();

                orderEvent.FillQuantity = filledQuantity * (order.Direction == OrderDirection.Buy ? 1 : -1);
                orderEvent.FillPrice = report.LastPx.getValue();
            }

            OnOrderEvent(orderEvent);
        }

        public void OnMessage(ExecutionReport report)
        {
            _fixInstance.OnMessage(report);
        }

        public void OnMessage(OrderCancelReject reject)
        {
            _fixInstance.OnMessage(reject);
        }
    }
}
