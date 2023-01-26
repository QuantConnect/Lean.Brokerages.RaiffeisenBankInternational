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
using QuantConnect.Tests;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.RBI.Fix;
using QuantConnect.RBI.Fix.Core;
using QuantConnect.RBI.Fix.Core.Implementations;
using QuantConnect.Securities;
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
            SenderCompId = "CLIENT1",
            TargetCompId = "SIMPLE",
            Host = "127.0.0.1",
            Port = 5080
        };
        private readonly QCAlgorithm _algorithm = new QCAlgorithm();
        private readonly LiveNodePacket _job = new LiveNodePacket();
        private readonly OrderProvider _orderProvider = new OrderProvider(new List<Order>());
        private readonly AggregationManager _aggregationManager = new AggregationManager();


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
        public void PlaceOrder(string ticker, decimal quantity, decimal price)
        {
            var symbolMapper = new RBISymbolMapper();
            var controller = new FixBrokerageController(symbolMapper);
            var messageHandler = new FixMessageHandler(_fixConfiguration, controller);

            using var fixInstance = new FixInstance(messageHandler, _fixConfiguration);

            fixInstance.Initialize();


            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId, _fixConfiguration.TargetCompId);

            fixInstance.OnLogon(sessionId);

            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity, DateTime.UtcNow, price);
            var actual = controller.PlaceOrder(order);


            var actualString = actual.ToString().Remove(10, 6);

            var msgSeqNum = actualString.Substring(18, 2);

            actualString = actualString.Remove(31, 25);
            
            var expectedString = $"8=FIX.4.2\u000135=D\u000134={msgSeqNum}\u000149=CLIENT1\u000156=SIMPLE\u000111={actual.ClOrdID}\u000115=\u000121=1\u000122=4\u000138={quantity}\u000140=1\u000144={price}\u000148={ticker} 2T\u000154=1\u000155={ticker}\u000160={actual.TransactTime}\u0001167=CS\u000110={actual.CheckSum()}\u0001";
            
            Assert.AreEqual(expectedString, actualString);
        }

        [Test]
        public void UpdateOrder()
        {
            var symbolMapper = new RBISymbolMapper();
            var controller = new FixBrokerageController(symbolMapper);
            var messageHandler = new FixMessageHandler(_fixConfiguration, controller);

            using var fixInstance = new FixInstance(messageHandler, _fixConfiguration);

            fixInstance.Initialize();


            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId, _fixConfiguration.TargetCompId);

            fixInstance.OnLogon(sessionId);
            
            var order = new MarketOrder(Symbol.Create("DLF", SecurityType.Equity, Market.USA), 1, DateTime.UtcNow, 500);
            controller.PlaceOrder(order);

            controller.UpdateOrder(order);
        }

        [Test]
        [TestCase("GOOCV", 2, 230)]
        public void PlaceOrderWithResponse(string ticker, decimal quantity, decimal price)
        {
            using (var brokerage =
                   new RBIBrokerage(_fixConfiguration, _aggregationManager, _orderProvider, _algorithm, _job))
            {
                var submittedEvent = new ManualResetEvent(false);

                brokerage.OrdersStatusChanged += (sender, e) =>
                {
                    if (e.Any(o => o.Status == OrderStatus.Submitted))
                    {
                        submittedEvent.Set();
                    }
                };

                brokerage.Connect();
                
                var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity, DateTime.UtcNow, price);

                _orderProvider.Add(order);

                brokerage.PlaceOrder(order);
                
                Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(20)));
            }
        }

        // protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
        // {
        //     return new RBIBrokerage();
        // }
        //
        // protected override Symbol Symbol { get; }
        // protected override SecurityType SecurityType { get; }
        // protected override bool IsAsync()
        // {
        //     return true;
        // }
        //
        // protected override decimal GetAskPrice(Symbol symbol)
        // {
        //     return 0;
        // }
    }
}