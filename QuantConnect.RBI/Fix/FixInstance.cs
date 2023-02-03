using System;
using System.Linq;
using System.Threading;
using QuantConnect.Packets;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using QuickFix.Transport;
using Log = QuantConnect.Logging.Log;
using Message = QuickFix.Message;

namespace QuantConnect.RBI.Fix;

public class FixInstance : MessageCracker, IApplication, IDisposable
{
    private readonly IFixMessageHandler _messageHandler;
    private readonly FixConfiguration _config;
    private SocketInitiator _initiator;
    private readonly SecurityExchangeHours _securityExchangeHours;

    private bool _isDisposed = false;

    public FixInstance(IFixMessageHandler messageHandler, FixConfiguration config)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _config = config;
        _securityExchangeHours =
            MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity);
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
        var connected = this.Connect();
        if (!connected)
        {
            Log.Trace("Fix connection is not established");
        }
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
        if (message.IsSetField(MsgType.TAG))
        {
            var msgType = message.GetString(MsgType.TAG);

            if (!msgType.Equals(MsgType.REJECT) && !msgType.Equals(MsgType.BUSINESS_MESSAGE_REJECT))
            {
                _messageHandler.EnrichMessage(message);
            }
        }
    }

    public void FromAdmin(Message message, SessionID sessionID)
    {
        _messageHandler.HandleAdminMessage(message, sessionID);
    }

    public void ToApp(Message message, SessionID sessionID)
    {
        // not implemented
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

    private bool IsExchangeOpen()
    {
        return _securityExchangeHours.IsOpen(DateTime.UtcNow.ConvertFromUtc(_securityExchangeHours.TimeZone), true);
    }

    public void OnMessage(ExecutionReport report)
    {
        _messageHandler.OnMessage(report, null);
    }

    public void OnMessage(OrderCancelReject reject)
    {
        _messageHandler.OnMessage(reject, null);
    }

    private bool Connect()
    {
        try
        {
            if (!_messageHandler.IsSessionReady() && IsExchangeOpen())
            {
                var count = 0;
                do
                {
                    _initiator.DisposeSafely();

                    var settings = _config.GetDefaultSessionSettings();
                    Log.Trace("Connecting started...");

                    var storeFactory = new FileStoreFactory(settings);
                    _initiator = new SocketInitiator(this, storeFactory, settings, new ScreenLogFactory(settings),
                        _messageHandler.MessageFactory);
                    _initiator.Start();

                    if (_messageHandler.IsSessionReady())
                    {
                        Log.Trace($"Connected FIX session");
                        return true;
                    }

                } while (!_messageHandler.IsSessionReady() && ++count <= 10);
            }
            else if (!IsExchangeOpen())
            {
                do
                {
                    _initiator.DisposeSafely();

                    var settings = _config.GetDefaultSessionSettings();
                    Log.Trace("Connecting started...");

                    var storeFactory = new FileStoreFactory(settings);
                    _initiator = new SocketInitiator(this, storeFactory, settings, new ScreenLogFactory(settings),
                        _messageHandler.MessageFactory);
                    _initiator.Start();

                    if (_messageHandler.IsSessionReady())
                    {
                        Log.Trace($"Connected FIX session");
                        return true;
                    }

                } while (!_messageHandler.IsSessionReady());
            }

            return true;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        return false;
    }
}