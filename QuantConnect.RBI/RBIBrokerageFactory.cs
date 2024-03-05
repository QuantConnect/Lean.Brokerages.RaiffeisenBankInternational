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

using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Brokerages.RBI.Fix;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.RBI
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
            { "rbi-account", Config.Get("rbi-account") },
            
            { "rbi-sender-comp-id", Config.Get("rbi-sender-comp-id") },
            { "rbi-target-comp-id", Config.Get("rbi-target-comp-id") },
            { "rbi-log-fix-messages", Config.Get("rbi-log-fix-messages") },
            { "rbi-on-behalf-of-comp-id", Config.Get("rbi-on-behalf-of-comp-id") },

            { "live-cash-balance", Config.Get("live-cash-balance")},
            { "live-holdings", Config.Get("live-holdings")},
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
            return new DefaultBrokerageModel();
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
                Host = Read<string>(job.BrokerageData, "rbi-host", errors),
                Port = Read<string>(job.BrokerageData, "rbi-port", errors),
                Account = Read<string>(job.BrokerageData, "rbi-account", errors),
                SenderCompId = Read<string>(job.BrokerageData, "rbi-sender-comp-id", errors),
                TargetCompId = Read<string>(job.BrokerageData, "rbi-target-comp-id", errors),
                OnBehalfOfCompID = Read<string>(job.BrokerageData, "rbi-on-behalf-of-comp-id", errors)
            };
            
            var logFixMessages = Read<bool>(job.BrokerageData, "rbi-log-fix-messages", new List<string>());

            Log.Trace(
                $"RBIBrokerageFactory.CreateBrokerage(): Host {fixConfig.Host}, Port {fixConfig.Port}, Account {fixConfig.Account}" +
                $" SenderCompId {fixConfig.SenderCompId}, TargetCompId {fixConfig.TargetCompId}," +
                $" OnBehalfOfCompId {fixConfig.OnBehalfOfCompID}. LogFixMessages {logFixMessages}");

            if (errors.Count > 0)
            {
                throw new Exception(string.Join(Environment.NewLine, errors));
            }

            var brokerage = new RBIBrokerage(
                fixConfig,
                algorithm.Transactions,
                algorithm,
                job,
                algorithm.Portfolio,
                logFixMessages
            );
            
            return brokerage;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        { }
    }
}