using System;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixSymbolController : IFixSymbolController
{
    private readonly IRBIFixConnection _session;

    public FixSymbolController(IRBIFixConnection session)
    {
        _session = session;
    }

    public bool SubscribeToSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }

    public bool UnsubscribeFromSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }
    
    public bool PlaceOrder(Order order)
    {
        var newOrder = new NewOrderSingle()
        {
            ClOrdID = new ClOrdID("DLF"),
            HandlInst = new HandlInst(HandlInst.MANUAL_ORDER),
            Symbol = new QuickFix.Fields.Symbol("DLF"),
            Side = new Side(Side.BUY),
            TransactTime = new TransactTime(DateTime.UtcNow),
            OrdType = new OrdType(OrdType.LIMIT)
        };
        
        newOrder.Set(new OrderQty(order.Quantity));
        newOrder.Set(new Price(order.Price));
        
        Log.Trace($"FixSymbolController.PlaceOrder(): sending order {order.Id}...");
        return _session.Send(newOrder);
    }
}