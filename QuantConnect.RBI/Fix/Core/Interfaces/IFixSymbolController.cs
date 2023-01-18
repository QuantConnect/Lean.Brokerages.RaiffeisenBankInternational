namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixSymbolController
{
    bool SubscribeToSymbol(Symbol symbol);

    bool UnsubscribeFromSymbol(Symbol symbol);
}