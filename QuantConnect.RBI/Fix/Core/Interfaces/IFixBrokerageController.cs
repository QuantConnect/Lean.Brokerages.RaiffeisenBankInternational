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
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixBrokerageController
{
    event EventHandler<ExecutionReport> ExecutionReport;
    
    void Register(IFixSymbolController controller);

    void Unregister(IFixSymbolController controller);

    bool PlaceOrder(Order order);

    bool CancelOrder(Order order);

    bool UpdateOrder(Order order);

    public List<Order> GetOpenOrders();

    void Receive(ExecutionReport report);
}