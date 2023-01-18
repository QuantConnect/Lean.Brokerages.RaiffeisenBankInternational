using System;
using QuantConnect.RBI.Fix.Core.Interfaces;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixBrokerageController : IFixBrokerageController
{
    private IFixSymbolController _symbolController;
    
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

    public void Subscribe(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }
    
    public void Unsubscribe(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }
}