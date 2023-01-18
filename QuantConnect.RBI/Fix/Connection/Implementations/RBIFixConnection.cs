using System;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuickFix;

namespace QuantConnect.RBI.Fix.Connection.Implementations;

public class RBIFixConnection : IRBIFixConnection
{
    private readonly Session _session;

    public RBIFixConnection(SessionID sessionId)
    {
        if (sessionId != null)
        {
            _session = Session.LookupSession(sessionId);
        }
        else
        {
            throw new ArgumentNullException(nameof(sessionId));
        }
    }

    public bool Send(Message message)
    {
        return _session.Send(message);
    }
}