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
using QuantConnect.Logging;
using QuantConnect.Orders.Fees;
using QuantConnect.RBI.Fix;
using QuantConnect.RBI.Fix.Core;
using QuantConnect.RBI.Fix.Core.Implementations;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuickFix.FIX42;

namespace QuantConnect.RBI
{
    [BrokerageFactory(typeof(RBIBrokerageFactory))]
    public class RBIBrokerage : Brokerage
    {
        private readonly IFixBrokerageController _fixBrokerageController;
        private readonly FixInstance _fixInstance;
        private readonly RBISymbolMapper _symbolMapper;

        private readonly IDataAggregator _aggregator;
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;
        private readonly IOrderProvider _orderProvider;
        private readonly ISecurityProvider _securityProvider;
        private readonly IAlgorithm _algorithm;
        private readonly LiveNodePacket _job;

        public event EventHandler<OrderEvent> OrderStatusChanged;

        public RBIBrokerage(
            FixConfiguration config,
            IDataAggregator aggregator,
            IOrderProvider orderProvider,
            ISecurityProvider securityProvider,
            IAlgorithm algorithm,
            LiveNodePacket job) : base("RBI")
        {
            _algorithm = algorithm;
            _job = job;
            _aggregator = aggregator;
            _orderProvider = orderProvider;
            _securityProvider = securityProvider;

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
        
        
        #region IDataQueueHandler
        
        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return null;
            }
        
            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);
        
            return enumerator;
        }
        
        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }
        
        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            throw new NotImplementedException();
        }
        
        #endregion
        
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
            return GetAccountHoldings(_job.BrokerageData, (_securityProvider as SecurityPortfolioManager)?.Securities.Values);
        }
        
        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public override List<CashAmount> GetCashBalance()
        {
            return GetCashBalance(_job.BrokerageData, (_securityProvider as SecurityPortfolioManager)?.CashBook);
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

        private bool CanSubscribe(Symbol symbol)
        {
            if (symbol.Value.IndexOfInvariant("universe", true) != -1)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        private bool Subscribe(IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        private bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }

        private void OnExecutionReport(object sender, ExecutionReport report)
        {
            Log.Trace($"OnExecutionReport(): {sender} sent {report}");

            var orderStatus = Utility.ConvertOrderStatus(report);

            var ordId = orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.UpdateSubmitted
                ? report.OrigClOrdID.getValue()
                : report.ClOrdID.getValue();

            var transactTime = report.TransactTime.getValue();

            var order = _orderProvider.GetOrderByBrokerageId(ordId);

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
            
            OnOrderEvent(orderEvent);
        }
    }
}
