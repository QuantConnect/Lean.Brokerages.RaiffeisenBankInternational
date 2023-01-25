using System;
using System.Linq;
using System.Threading;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.Util;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Transport;
using Message = QuickFix.Message;

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
        ScreenLogFactory logFactory = new ScreenLogFactory(settings); 
        var messageFactory = new DefaultMessageFactory(); 

        _initiator = new SocketInitiator(this, storeFactory, settings, logFactory, messageFactory);
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
        _initiator.Start();
        Thread.Sleep(3000);
    }

    public void Terminate()
    {
        if (!_initiator.IsStopped)
        {
            _initiator.Stop();
        }
    }

    public void ToAdmin(Message message, SessionID sessionID)
    {
        _messageHandler.EnrichMessage(message);
    }

    public void FromAdmin(Message message, SessionID sessionID)
    {
        _messageHandler.HandleAdminMessage(message, sessionID);
    }

    public void ToApp(Message message, SessionID sessionID)
    {
        Console.WriteLine("toapp");
    }

    public void FromApp(Message message, SessionID sessionID)
    {
        _messageHandler.Handle(message, sessionID);
    }

    public void OnCreate(SessionID sessionID)
    {
        
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