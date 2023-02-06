using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixBrokerageController : IFixBrokerageController
{
    private readonly Dictionary<string, ExecutionReport> _executions = new();
    private IFixSymbolController _symbolController;

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
            throw new ArgumentNullException($"Handler has not been registered");
        }
        
        return _symbolController.PlaceOrder(order);
    }

    public bool CancelOrder(Order order)
    {
        if (_symbolController == null)
        {
            throw new ArgumentNullException($"Handler has not been registered");
        }
        
        return _symbolController.CancelOrder(order);
    }

    public bool UpdateOrder(Order order)
    {
        if (_symbolController == null)
        {
            throw new ArgumentNullException($"Handler has not been registered");
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
            _executions.Remove(orderId);
        }

        ExecutionReport?.Invoke(this, execution);
    }

    private Order ConvertOrder(ExecutionReport report)
    {
        var ticker = report.Symbol.getValue();
        var securityType = _symbolMapper.GetLeanSecurityType(report.SecurityType.getValue());

        var symbol = Symbol.Create(ticker, securityType, Market.USA);

        var orderQty = report.OrderQty.getValue();
        var orderSide = report.Side.getValue();

        if (orderSide == Side.SELL)
        {
            orderQty = -orderQty;
        }
        var time = report.TransactTime.getValue();
        var orderType = Utility.ConvertOrderType(report.OrdType.getValue());

        Order order = null;
        
        switch (orderType)
        {
            case OrderType.Market:
                order = new MarketOrder();
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
        
        order?.BrokerId.Add(report.ClOrdID.getValue());

        return order;
    }
}