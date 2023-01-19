using QuantConnect.Orders;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixSymbolController
{
    bool SubscribeToSymbol(Symbol symbol);

    bool UnsubscribeFromSymbol(Symbol symbol);

    bool PlaceOrder(Order order);
}