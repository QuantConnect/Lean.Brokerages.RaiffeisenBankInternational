using QuickFix;

namespace QuantConnect.RBI.Fix.Connection.Interfaces;

public interface IRBIFixConnection
{
    bool Send(Message message);
}