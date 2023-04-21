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
using QuantConnect.Tests.Brokerages;

namespace QuantConnect.RBI.Tests
{
    [TestFixture]
    [Ignore("Requires valid config.json")]
    public class RBIBrokerageTests
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

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void ConnectionTest(string ticker, decimal quantity, decimal price)
        {
            using var brokerage = CreateBrokerage();
            
            brokerage.Connect();
            
            Thread.Sleep(10000);

            Assert.IsTrue(brokerage.IsConnected);
            
            brokerage.Disconnect();
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void PlaceOrderWithResponse(string ticker, decimal quantity, decimal price)
        {
            using var brokerage = CreateBrokerage();
            var submittedEvent = new ManualResetEvent(false);

            brokerage.OrdersStatusChanged += (sender, e) =>
            {
                if (e.Single().Status == OrderStatus.Filled)
                {
                    submittedEvent.Set();
                }
            };
        
            brokerage.Connect();
            
            Thread.Sleep(1000);

            Assert.IsTrue(brokerage.IsConnected);
        
            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);
        
            var properties = order.Properties as OrderProperties;
        
            properties.Exchange = Exchange.EDGA;
        
            _orderProvider.Add(order);
        
            Assert.IsTrue(brokerage.PlaceOrder(order));

            Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(20)));

            brokerage.Disconnect();
        }

        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void ModifyOrder(string ticker, decimal quantity, decimal price)
        {
            using var brokerage = CreateBrokerage();
            var replacedEvent = new ManualResetEvent(false);
            brokerage.OrdersStatusChanged += (sender, e) =>
            {
        
                if (e.Single().Status == OrderStatus.UpdateSubmitted)
                {
                    replacedEvent.Set();
                }
            };
        
            brokerage.Connect();
            Thread.Sleep(1000);
            Assert.IsTrue(brokerage.IsConnected);
            
            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);
            var properties = order.Properties as OrderProperties;
            properties.Exchange = Exchange.EDGA;
            _orderProvider.Add(order);
        
            Assert.IsTrue(brokerage.PlaceOrder(order));
            Thread.Sleep(5000);
            Assert.IsTrue(brokerage.UpdateOrder(order));
            Assert.IsTrue(replacedEvent.WaitOne(TimeSpan.FromSeconds(20)));
            
            brokerage.Disconnect();
        }
        
        
        [Test]
        [TestCase("GOOCV", 210, 230)]
        public void CancelOrder(string ticker, decimal quantity, decimal price)
        {
            using var brokerage = CreateBrokerage();
            var canceledEvent = new ManualResetEvent(false);
        
            brokerage.OrdersStatusChanged += (sender, e) =>
            { 
                if (e.Single().Status == OrderStatus.Canceled)
                {
                    canceledEvent.Set();
                }
            };
        
            brokerage.Connect();
            Thread.Sleep(1000);
            Assert.IsTrue(brokerage.IsConnected);
        
            var order = new MarketOrder(Symbol.Create(ticker, SecurityType.Equity, Market.USA), quantity,
                DateTime.UtcNow, price);
            var properties = order.Properties as OrderProperties;
            properties.Exchange = Exchange.EDGA;
            _orderProvider.Add(order);
        
            Assert.IsTrue(brokerage.PlaceOrder(order));
            Thread.Sleep(5000);
            Assert.IsTrue(brokerage.CancelOrder(order));
            Assert.IsTrue(canceledEvent.WaitOne(TimeSpan.FromSeconds(20)));
            
            brokerage.Disconnect();
        }

        private RBIBrokerage CreateBrokerage()
        {
            return new RBIBrokerage(_fixConfiguration, _orderProvider, _algorithm, _job, TestGlobals.MapFileProvider,
                new SecurityProvider(), false);
        }
    }
}