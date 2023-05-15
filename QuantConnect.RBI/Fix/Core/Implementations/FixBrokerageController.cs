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

using System.Collections.Concurrent;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixBrokerageController : IFixBrokerageController
{
    private readonly ConcurrentDictionary<string, ExecutionReport> _executions = new();
    private IFixSymbolController _symbolController;

    public event EventHandler<OrderCancelReject> CancelReject;

    public event EventHandler<ExecutionReport> ExecutionReport;

    public void Register(IFixSymbolController controller)
    {
        if (_symbolController != null)
        {
            throw new Exception(
                $"A handler has already been registered: {_symbolController.GetType().FullName}");
        }

        _symbolController = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public void Unregister(IFixSymbolController controller)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        if (_symbolController == null || controller != _symbolController)
        {
            throw new Exception(
                $"The handler has not been registered: {controller.GetType().FullName}");
        }

        _symbolController = null;
    }

    public bool PlaceOrder(Order order)
    {
        if (_symbolController == null)
        {
            return false;
        }
        
        return _symbolController.PlaceOrder(order);
    }

    public bool CancelOrder(Order order)
    {
        if (_symbolController == null)
        {
            return false;
        }
        
        return _symbolController.CancelOrder(order);
    }

    public bool UpdateOrder(Order order)
    {
        if (_symbolController == null)
        {
            return false;
        }

        return _symbolController.UpdateOrder(order);
    }

    public List<Order> GetOpenOrders()
    {
        throw new NotImplementedException();
    }
    
    public void Receive(ExecutionReport execution)
    {
        var orderId = execution.ClOrdID.getValue();
        var orderStatus = execution.OrdStatus.getValue();
        if (orderStatus != OrdStatus.REJECTED)
        {
            _executions[orderId] = execution;
        }
        else
        {
            _executions.Remove(orderId, out _);
        }

        ExecutionReport?.Invoke(this, execution);
    }

    public void Receive(OrderCancelReject cancelReject)
    {
        CancelReject?.Invoke(this, cancelReject);
    }
}