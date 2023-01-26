using System;
using System.Collections.Generic;
using QuantConnect.Orders;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixBrokerageController
{
    event EventHandler<ExecutionReport> ExecutionReport;
    
    void Register(IFixSymbolController controller);

    void Unregister(IFixSymbolController controller);

    void Subscribe(Symbol symbol);

    void Unsubscribe(Symbol symbol);

    bool PlaceOrder(Order order);

    bool CancelOrder(Order order);

    bool UpdateOrder(Order order);

    public List<Order> GetOpenOrders();

    void Receive(ExecutionReport report);
}