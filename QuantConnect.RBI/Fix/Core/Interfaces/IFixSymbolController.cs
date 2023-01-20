using QuantConnect.Orders;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixSymbolController
{
    bool SubscribeToSymbol(Symbol symbol);

    bool UnsubscribeFromSymbol(Symbol symbol);

    NewOrderSingle PlaceOrder(Order order);

    bool CancelOrder(Order order);

    OrderCancelReplaceRequest UpdateOrder(Order order);
}