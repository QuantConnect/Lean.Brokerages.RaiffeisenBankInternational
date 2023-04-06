/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuickFix;
using QuickFix.FIX42;
using QuickFix.Transport;
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
    private volatile bool _connected;

    private bool _isDisposed;

    public FixInstance(IFixMessageHandler messageHandler, FixConfiguration config, bool logFixMesssages)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _config = config;
        _securityExchangeHours =
            MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity);
        _logFactory = new LogFactory.LogFactory(logFixMesssages);
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

    /// <summary>
    /// All outbound admin level messages pass through this callback.
    /// </summary>
    /// <param name="message">Message</param>
    /// <param name="sessionID">SessionID</param>
    public void ToAdmin(Message message, SessionID sessionID)
    {
        _messageHandler.EnrichMessage(message);
    }

    /// <summary>
    /// Every inbound admin level message will pass through this method, such as heartbeats, logons, and logouts.
    /// </summary>
    /// <param name="message">Message</param>
    /// <param name="sessionID">SessionID</param>
    public void FromAdmin(Message message, SessionID sessionID)
    {
        _messageHandler.HandleAdminMessage(message, sessionID);
    }

    /// <summary>
    /// All outbound application level messages pass through this callback before they are sent. 
    /// If a tag needs to be added to every outgoing message, this is a good place to do that.
    /// </summary>
    /// <param name="message">Message</param>
    /// <param name="sessionID">SessionID</param>
    public void ToApp(Message message, SessionID sessionID)
    { }

    /// <summary>
    /// Every inbound application level message will pass through this method, such as orders, executions, security definitions, and market data
    /// </summary>
    /// <param name="message"></param>
    /// <param name="sessionID"></param>
    public void FromApp(Message message, SessionID sessionID)
    {
        _messageHandler.Handle(message, sessionID);
    }

    public void OnCreate(SessionID sessionID)
    { }

    /// <summary>
    /// Notifies when a successful logon has completed.
    /// </summary>
    /// <param name="sessionID">SessionID</param>
    public void OnLogout(SessionID sessionID)
    {
        _messageHandler.OnLogout(sessionID);
    }

    /// <summary>
    /// Notifies when a session is offline - either from an exchange of logout messages or network connectivity loss.
    /// </summary>
    /// <param name="sessionID">SessionID</param>
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
}