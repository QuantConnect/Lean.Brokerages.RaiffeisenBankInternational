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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Tests;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.RBI.Fix;
using QuantConnect.RBI.Fix.Core;
using QuantConnect.RBI.Fix.Core.Implementations;
using QuantConnect.Tests.Brokerages;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Tests
{
    [TestFixture]
    public partial class RBIBrokerageTests
    {
        private readonly FixConfiguration _fixConfiguration = new()
        {
            SenderCompId = Config.Get("rbi-sender-comp-id"),
            TargetCompId = Config.Get("rbi-target-comp-id"),
            Host = Config.Get("rbi-host"),
            Port = Config.Get("rbi-port")
        };
        private readonly QCAlgorithm _algorithm = new ();
        private readonly LiveNodePacket _job = new ();
        private readonly OrderProvider _orderProvider = new (new List<Order>());


        /// <summary>
        /// Provides the data required to test each order type in various cases
        /// </summary>
        private static TestCaseData[] OrderParameters()
        {
            return new[]
            {
                new TestCaseData(new MarketOrderTestParameters(Symbols.BTCUSD)).SetName("MarketOrder"),
                new TestCaseData(new LimitOrderTestParameters(Symbols.BTCUSD, 10000m, 0.01m)).SetName("LimitOrder"),
                new TestCaseData(new StopMarketOrderTestParameters(Symbols.BTCUSD, 10000m, 0.01m)).SetName("StopMarketOrder"),
                new TestCaseData(new StopLimitOrderTestParameters(Symbols.BTCUSD, 10000m, 0.01m)).SetName("StopLimitOrder"),
                new TestCaseData(new LimitIfTouchedOrderTestParameters(Symbols.BTCUSD, 10000m, 0.01m)).SetName("LimitIfTouchedOrder")
            };
        }
        
        [Test]
        [TestCase("AAPL",5, 500)]
        [TestCase("DLF", 10, 10000)]
        [TestCase("GOOCV", 2, 230)]
        [Ignore("Manual test for receiving fix protocol properly")]
        public void PlaceOrder(string ticker, decimal quantity, decimal price)
        {
            var symbolMapper = new RBISymbolMapper();
            var controller = new FixBrokerageController(symbolMapper);
            var messageHandler = new FixMessageHandler(_fixConfiguration, controller);

            using var fixInstance = new FixInstance(messageHandler, _fixConfiguration);

            fixInstance.Initialize();


            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId,
                _fixConfiguration.TargetCompId);

            fixInstance.OnLogon(sessionId);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);
            var actual = controller.PlaceOrder(order);


            var actualString = actual.ToString().Remove(10, 6);

            var msgSeqNum = actualString.Substring(18, 2);

            actualString = actualString.Remove(31, 25);
        }

        [Test]
        public void UpdateOrder()
        {
            var symbolMapper = new RBISymbolMapper();
            var controller = new FixBrokerageController(symbolMapper);
            var messageHandler = new FixMessageHandler(_fixConfiguration, controller);

            using var fixInstance = new FixInstance(messageHandler, _fixConfiguration);

            fixInstance.Initialize();

            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId,
                _fixConfiguration.TargetCompId);

            fixInstance.OnLogon(sessionId);

            var order = new MarketOrder(
                Symbol.Create("DLF", SecurityType.Equity, Market.USA),
                1,
                DateTime.UtcNow,
                500);
            controller.PlaceOrder(order);

            controller.UpdateOrder(order);
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void PlaceOrderWithResponse(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();
            var submittedEvent = new ManualResetEvent(false);
            var pendingEvent = new ManualResetEvent(false);
                
            brokerage.OrdersStatusChanged += (sender, e) =>
            {
                if (e.Single().Status == OrderStatus.Submitted)
                {
                    submittedEvent.Set();
                }

                if (e.Single().Status == OrderStatus.New)
                {
                    pendingEvent.Set();
                }
            };

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            var properties = order.Properties as OrderProperties;

            properties.Exchange = Exchange.EDGA;

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            var firstReport = new ExecutionReport()
            {
                OrdStatus = new OrdStatus('A'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('A'),
                TransactTime = new TransactTime(DateTime.UtcNow),
            };

            brokerage.OnMessage(firstReport);

            var secondReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('0'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('0'),
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            brokerage.OnMessage(secondReport);

            Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(20)));
            Assert.IsTrue(pendingEvent.WaitOne(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [TestCase("GOOCV", 220, 230)]
        public void PlaceOrderWithPartialFill(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();
            var submittedEvent = new ManualResetEvent(false);
            var partialFilledEvent = new ManualResetEvent(false);
            var filledEvent = new ManualResetEvent(false);
            var pendingEvent = new ManualResetEvent(false);

            brokerage.OrdersStatusChanged += (sender, e) =>
            {
                if (e.Single().Status == OrderStatus.Submitted)
                {
                    submittedEvent.Set();
                }

                else if (e.Single().Status == OrderStatus.New)
                {
                    pendingEvent.Set();
                }
                
                else if (e.Single().Status == OrderStatus.PartiallyFilled)
                {
                    partialFilledEvent.Set();
                }

                else if (e.Single().Status == OrderStatus.Filled)
                {
                    filledEvent.Set();
                }
            };

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            var pendingReport = new ExecutionReport()
            {
                OrdStatus = new OrdStatus('A'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('A'),
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            brokerage.OnMessage(pendingReport);

            var newReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('0'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('0'),
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            brokerage.OnMessage(newReport);

            var partiallyFilledReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('1'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('1'),
                TransactTime = new TransactTime(DateTime.UtcNow),
                LastShares = new LastShares(200),
                CumQty = new CumQty(100),
                LastPx = new LastPx(300)
            };

            brokerage.OnMessage(partiallyFilledReport);

            var filledReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('2'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('2'),
                TransactTime = new TransactTime(DateTime.UtcNow),
                LastShares = new LastShares(20),
                CumQty = new CumQty(220),
                LastPx = new LastPx(250)
            };

            brokerage.OnMessage(filledReport);

            Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(pendingEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(partialFilledEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(filledEvent.WaitOne(TimeSpan.FromSeconds(10)));
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void PlaceOrderWithReject(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();
            var rejectedEvent = new ManualResetEvent(false);
            var pendingEvent = new ManualResetEvent(false);

            brokerage.OrdersStatusChanged += (sender, e) =>
            {
                if (e.Single().Status == OrderStatus.Invalid)
                {
                    rejectedEvent.Set();
                }

                if (e.Single().Status == OrderStatus.New)
                {
                    pendingEvent.Set();
                }
            };

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            var pendingReport = new ExecutionReport()
            {
                OrdStatus = new OrdStatus('A'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('A'),
                TransactTime = new TransactTime(DateTime.UtcNow),
            };

            brokerage.OnMessage(pendingReport);

            var rejectedReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('8'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("12345"),
                ExecType = new ExecType('8'),
                TransactTime = new TransactTime(DateTime.UtcNow),
                Text = new Text("Dynamic  limits:  limit  would  be  breached,  Order price(273) is below minimum allowed soft price (700.5)")
            };

            brokerage.OnMessage(rejectedReport);

            Assert.IsTrue(pendingEvent.WaitOne(TimeSpan.FromSeconds(20)));
            Assert.IsTrue(rejectedEvent.WaitOne(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void ModifyOrder(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();
            var pendingReplaceEvent = new ManualResetEvent(false);
            var replacedEvent = new ManualResetEvent(false);

            brokerage.OrdersStatusChanged += (sender, e) =>
            {
                if (e.Single().Status == OrderStatus.Invalid)
                {
                    pendingReplaceEvent.Set();
                }

                if (e.Single().Status == OrderStatus.UpdateSubmitted)
                {
                    replacedEvent.Set();
                }
            };

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            brokerage.UpdateOrder(order);

            var pendingReport = new ExecutionReport()
            {
                OrdStatus = new OrdStatus('E'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("123456"),
                OrigClOrdID = new OrigClOrdID("12345"),
                ExecType = new ExecType('E'),
                TransactTime = new TransactTime(DateTime.UtcNow),
            };

            brokerage.OnMessage(pendingReport);

            var rejectedReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('5'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("123456"),
                ExecType = new ExecType('5'),
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            brokerage.OnMessage(rejectedReport);

            Assert.IsTrue(pendingReplaceEvent.WaitOne(TimeSpan.FromSeconds(20)));
            Assert.IsTrue(replacedEvent.WaitOne(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void ModifyOrderReject(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            brokerage.UpdateOrder(order);

            var rejection = new OrderCancelReject
            {
                CxlRejReason = new CxlRejReason(0),
                CxlRejResponseTo = new CxlRejResponseTo('2'),
                Text = new Text("FIX IN: Order is already filled or canceled")
            };

            brokerage.OnMessage(rejection);
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void CancelOrder(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();
            var canceledEvent = new ManualResetEvent(false);

            brokerage.OrdersStatusChanged += (sender, e) =>
            { 
                if (e.Single().Status == OrderStatus.Canceled)
                {
                    canceledEvent.Set();
                }
            };

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            brokerage.CancelOrder(order);

            var rejectedReport = new ExecutionReport
            {
                OrdStatus = new OrdStatus('4'),
                OrderID = new OrderID(_orderProvider.GetOrders(o => true).FirstOrDefault()?.Id.ToString()),
                ClOrdID = new ClOrdID("123456"),
                OrigClOrdID = new OrigClOrdID("12345"),
                ExecType = new ExecType('4'),
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            brokerage.OnMessage(rejectedReport);
            
            Assert.IsTrue(canceledEvent.WaitOne(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void CancelOrderReject(string ticker, decimal quantity, decimal price)
        {
            using var brokerage =
                CreateBrokerage();

            brokerage.Connect(_fixConfiguration);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);

            _orderProvider.Add(order);

            brokerage.PlaceOrder(order);

            brokerage.UpdateOrder(order);

            var rejection = new OrderCancelReject
            {
                CxlRejReason = new CxlRejReason(0),
                CxlRejResponseTo = new CxlRejResponseTo('2'),
                Text = new Text("FIX IN: Order is already filled or canceled")
            };

            brokerage.OnMessage(rejection);
        }

        private RBIBrokerage CreateBrokerage()
        {
            return new RBIBrokerage(_fixConfiguration, _orderProvider, _algorithm, _job);
        }
    }
}