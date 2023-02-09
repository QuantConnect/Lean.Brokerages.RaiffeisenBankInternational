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
    private readonly LogFactory.LogFactory _logFactory;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly ManualResetEvent _loginEvent = new (false);
    private volatile bool _connected;

    private bool _isDisposed = false;

    public FixInstance(IFixMessageHandler messageHandler, FixConfiguration config)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _config = config;
        _securityExchangeHours =
            MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity);
        _logFactory = new LogFactory.LogFactory();
    }
    
    public bool IsConnected()
    {
        return _connected && !_isDisposed;
    }

    public void Initialize()
    {
        var settings = _config.GetDefaultSessionSettings();
        var storeFactory = new FileStoreFactory(settings);
        _initiator = new SocketInitiator(this, storeFactory, settings, _logFactory,
            _messageHandler.MessageFactory);
        _initiator.Start();
        // _cancellationTokenSource = new CancellationTokenSource();
        // _connected = Connect();
        // Task.Factory.StartNew(() =>
        // {
        //     var retry = 0;
        //     var timeoutLoop = TimeSpan.FromMinutes(1);
        //     while (!_cancellationTokenSource.Token.IsCancellationRequested)
        //     {
        //         if (_cancellationTokenSource.Token.WaitHandle.WaitOne(timeoutLoop))
        //         {
        //             break;
        //         }
        //
        //         if (!Connect())
        //         {
        //            Log.Error($"FixInstance(): connection failed");
        //         }
        //         else
        //         {
        //             retry = 0;
        //         }
        //     }
    // });
    }

    public void Terminate()
    {
        if (!_initiator.IsStopped)
        {
            _initiator.Stop();
            _initiator.DisposeSafely();
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
        _loginEvent.Set();
    }

    public void OnLogon(SessionID sessionID)
    {
        _messageHandler.OnLogon(sessionID);
        _loginEvent.Set();
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
                    _loginEvent.Reset();
                    
                    var settings = _config.GetDefaultSessionSettings();
                    var sessionId = settings.GetSessions().Single();
                    Log.Trace("Connecting started...");
        
                    var storeFactory = new FileStoreFactory(settings);
                    _initiator = new SocketInitiator(this, storeFactory, settings, _logFactory,
                        _messageHandler.MessageFactory);
                    _initiator.Start();

                    if (!_loginEvent.WaitOne(TimeSpan.FromSeconds(15), _cancellationTokenSource.Token))
                    {
                        Log.Error($"FixInstance.TryConnect({sessionId}): Timeout initializing FIX session.");
                    }
        
                    if (_messageHandler.IsSessionReady())
                    {
                        Log.Trace($"Connected FIX session");
                        return true;
                    }
        
                } while (!_messageHandler.IsSessionReady() && ++count <= 10);

                return false;
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