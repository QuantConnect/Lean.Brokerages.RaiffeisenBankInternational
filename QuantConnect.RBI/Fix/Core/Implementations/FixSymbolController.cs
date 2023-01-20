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
    private readonly RBISymbolMapper _symbolMapper;

    public FixSymbolController(IRBIFixConnection session)
    {
        _session = session;
        _symbolMapper = new RBISymbolMapper();
    }

    public bool SubscribeToSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }

    public bool UnsubscribeFromSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }
    
    public NewOrderSingle PlaceOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);

        var securityType = new QuickFix.Fields.SecurityType(_symbolMapper.GetBrokerageSecurityType(order.Symbol.SecurityType));
        
        var newOrder = new NewOrderSingle()
        {
            ClOrdID = new ClOrdID(order.Id.ToString()),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PUBLIC_BROKER_INTERVENTION_OK),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            Side = new Side(Side.BUY),
            //TransactTime = new TransactTime(DateTime.UtcNow),
            OrdType = new OrdType(OrdType.LIMIT),
            SecurityType = securityType
        };
        
        newOrder.Set(new OrderQty(order.Quantity));
        newOrder.Set(new Price(order.Price));
        
        Log.Trace($"FixSymbolController.PlaceOrder(): sending order {order.Id}...");
        _session.Send(newOrder);
        return newOrder;
    }
}