namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixBrokerageController
{
    void Register(IFixSymbolController controller);

    void Unregister(IFixSymbolController controller);

    void Subscribe(Symbol symbol);

    void Unsubscribe(Symbol symbol);
}