using QuantConnect.Orders;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixSymbolController
{
    bool PlaceOrder(Order order);

    bool CancelOrder(Order order);

    bool UpdateOrder(Order order);
}