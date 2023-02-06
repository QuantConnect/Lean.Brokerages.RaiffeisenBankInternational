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
using QuantConnect.Packets;
using QuantConnect.Brokerages;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.RBI.Fix;

namespace QuantConnect.RBI
{
    /// <summary>
    /// Provides a template implementation of BrokerageFactory
    /// </summary>
    public class RBIBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// Gets the brokerage data required to run the brokerage from configuration/disk
        /// </summary>
        /// <remarks>
        /// The implementation of this property will create the brokerage data dictionary required for
        /// running live jobs. See <see cref="IJobQueueHandler.NextJob"/>
        /// </remarks>
        public override Dictionary<string, string> BrokerageData => new()
        {
            { "rbi-host", Config.Get("rbi-host") },
            { "rbi-port", Config.Get("rbi-port") },
            {"rbi-sender-comp-id", Config.Get("rbi-sender-comp-id") },
            {"rbi-target-comp-id", Config.Get("rbi-target-comp-id") }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="RBIBrokerageFactory"/> class
        /// </summary>
        public RBIBrokerageFactory() : base(typeof(RBIBrokerage))
        {
        }

        /// <summary>
        /// Gets a brokerage model that can be used to model this brokerage's unique behaviors
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        public override IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new IBrokerage instance
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();
            
            var fixConfig = new FixConfiguration()
            {
                Host = Read<string>(job.BrokerageData, "wolverine-host", errors),
                Port = Read<string>(job.BrokerageData, "wolverine-port", errors),
                SenderCompId = Read<string>(job.BrokerageData, "wolverine-sender-comp-id", errors),
                TargetCompId = Read<string>(job.BrokerageData, "wolverine-target-comp-id", errors),
            };

            Log.Trace(
                $"CreateBrokerage(): Host {fixConfig.Host}, Port {fixConfig.Port}," +
                $" SenderCompId {fixConfig.SenderCompId}, TargetCompId {fixConfig.TargetCompId}");

            if (errors.Count > 0)
            {
                throw new Exception(string.Join(Environment.NewLine, errors));
            }

            var brokerage = new RBIBrokerage(fixConfig, algorithm.Transactions, algorithm, job);

            return brokerage;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            
        }
    }
}