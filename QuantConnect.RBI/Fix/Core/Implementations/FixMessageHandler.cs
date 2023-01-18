using System;
using System.Collections.Concurrent;
using QuantConnect.RBI.Fix.Connection.Implementations;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuickFix;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixMessageHandler : IFixMessageHandler
{
    private readonly FixConfiguration _config;
    private readonly IFixBrokerageController _brokerageController;
    private IFixSymbolController _fixSymbolController;

    public FixMessageHandler(FixConfiguration config, IFixBrokerageController brokerageController)
    {
        _config = config;
        _brokerageController = brokerageController;
    }

    public bool AreSessionsReady()
    {
        throw new System.NotImplementedException();
    }

    public IMessageFactory MessageFactory { get; set; }
    
    public void Handle(Message message, SessionID sessionId)
    {
        throw new System.NotImplementedException();
    }

    public void EnrichMessage(Message message)
    {
        throw new System.NotImplementedException();
    }

    public void OnLogon(SessionID sessionId)
    {
        Logging.Log.Trace($"OnLogon(): Adding handler for SessionId {sessionId}");

        if (sessionId.SenderCompID == _config.SenderCompId && sessionId.TargetCompID == _config.TargetCompId)
        {
            _fixSymbolController = new FixSymbolController();
        }
        else
        {
            throw new Exception($"Unknown session SenderCompId: {sessionId.SenderCompID}");
        }
    }

    public void OnLogout(SessionID sessionId)
    {
        throw new System.NotImplementedException();
    }
}