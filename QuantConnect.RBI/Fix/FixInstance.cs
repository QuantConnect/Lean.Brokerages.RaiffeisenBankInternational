using System;
using System.Linq;
using System.Threading;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.Util;
using QuickFix;
using QuickFix.Transport;

namespace QuantConnect.RBI.Fix;

public class FixInstance : MessageCracker, IApplication, IDisposable
{
    private readonly IFixMessageHandler _messageHandler;
    private readonly FixConfiguration _config;
    private readonly SocketInitiator _initiator;

    private bool _isDisposed = false;

    public FixInstance(IFixMessageHandler messageHandler, FixConfiguration config)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _config = config;

        var settings = _config.GetDefaultSessionSettings();
        var storeFactory = new FileStoreFactory(settings);

        _initiator = new SocketInitiator(this, storeFactory, settings);
    }
    
    public bool IsConnected()
    {
        return !_initiator.IsStopped &&
               _initiator.GetSessionIDs()
                   .Select(Session.LookupSession)
                   .All(session => session != null && session.IsLoggedOn);
    }

    public void Initialize()
    {
        if (_initiator.IsStopped)
        {
            _initiator.Start();

            var startTime = DateTime.UtcNow;
            while (!IsConnected() || !_messageHandler.IsSessionReady())
            {
                if (DateTime.UtcNow > startTime.AddSeconds(60))
                {
                    throw new TimeoutException("Timeout initializing FIX sessions.");
                }

                Thread.Sleep(1000);
            }
        }
    }

    public void ToAdmin(Message message, SessionID sessionID)
    {
        throw new NotImplementedException();
    }

    public void FromAdmin(Message message, SessionID sessionID)
    {
    }

    public void ToApp(Message message, SessionID sessionID)
    {
        throw new NotImplementedException();
    }

    public void FromApp(Message message, SessionID sessionID)
    {
        throw new NotImplementedException();
    }

    public void OnCreate(SessionID sessionID)
    {
        throw new NotImplementedException();
    }

    public void OnLogout(SessionID sessionID)
    {
        _messageHandler.OnLogout(sessionID);
    }

    public void OnLogon(SessionID sessionID)
    {
        _messageHandler.OnLogon(sessionID);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _initiator.DisposeSafely();
    }
}