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
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixBrokerageController : IFixBrokerageController
{
    private readonly ConcurrentDictionary<string, ExecutionReport> _executions = new();
    private IFixSymbolController _symbolController;
    private bool _isControllerRegistered;

    private readonly RBISymbolMapper _symbolMapper;

    public FixBrokerageController(RBISymbolMapper mapper)
    {
        _symbolMapper = mapper;
    }

    public event EventHandler<ExecutionReport> ExecutionReport;

    public void Register(IFixSymbolController controller)
    {
        if (_symbolController != null)
        {
            throw new Exception(
                $"A handler has already been registered: {_symbolController.GetType().FullName}");
        }

        _symbolController = controller ?? throw new ArgumentNullException(nameof(controller));
        _isControllerRegistered = true;
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
        _isControllerRegistered = false;
    }

    public bool PlaceOrder(Order order)
    {
        if (!_isControllerRegistered)
        {
            Log.Error("No controller has been registered, error with LogOn");
            return false;
        }
        
        return _symbolController.PlaceOrder(order);
    }

    public bool CancelOrder(Order order)
    {
        if (!_isControllerRegistered)
        {
            Log.Error("No controller has been registered, error with LogOn");
            return false;
        }
        
        return _symbolController.CancelOrder(order);
    }

    public bool UpdateOrder(Order order)
    {
        if (!_isControllerRegistered)
        {
            Log.Error("No controller has been registered, error with LogOn");
            return false;
        }

        return _symbolController.UpdateOrder(order);
    }

    public List<Order> GetOpenOrders()
    {
        return _executions.Values.Select(ConvertOrder).Where(o => o.Status.IsOpen()).ToList();
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

    private Order ConvertOrder(ExecutionReport report)
    {
        var ticker = report.Symbol.getValue();
        var securityType = _symbolMapper.GetLeanSecurityType(report.SecurityType.getValue());

        var symbol = _symbolMapper.GetLeanSymbol(ticker, securityType, Market.USA);

        var orderQty = report.OrderQty.getValue();
        var orderSide = report.Side.getValue();

        if (orderSide == Side.SELL)
        {
            orderQty = -orderQty;
        }
        var time = report.TransactTime.getValue();
        var orderType = Utility.ConvertOrderType(report.OrdType.getValue());

        Order order = new MarketOrder(symbol, orderQty, time, report.Price.Obj );
        
        switch (orderType)
        {
            case OrderType.Market:
                order = new MarketOrder(symbol, orderQty, time, report.Price.Obj );
                break;

            case OrderType.Limit:
            {
                var limitPrice = report.Price.getValue();
                order = new LimitOrder(symbol, orderQty, limitPrice, time);
            }
                break;

            case OrderType.StopMarket:
            {
                var stopPrice = report.StopPx.getValue();
                order = new LimitOrder(symbol, orderQty, stopPrice, time);
            }
                break;

            case OrderType.StopLimit:
            {
                var limitPrice = report.Price.getValue();
                var stopPrice = report.StopPx.getValue();
                order = new StopLimitOrder(symbol, orderQty, stopPrice, limitPrice, time);
            }
                break;

            case OrderType.LimitIfTouched:
            {
                var limitPrice = report.Price.getValue();
                var stopPrice = report.StopPx.getValue();
                order = new LimitIfTouchedOrder(symbol, orderQty, stopPrice, limitPrice, time);
            }
                break;
        }
        
        order.BrokerId.Add(report.ClOrdID.getValue());

        return order;
    }
}