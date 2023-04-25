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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Implementations;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using Log = QuantConnect.Logging.Log;
using Message = QuickFix.Message;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixMessageHandler : MessageCracker, IFixMessageHandler
{
    private readonly IFixBrokerageController _brokerageController;
    private readonly ISecurityProvider _securityProvider;
    private readonly RBISymbolMapper _symbolMapper;
    private readonly string _account;
    private readonly ConcurrentDictionary<SessionID, IFixSymbolController> _sessionHandlers = new();

    public IMessageFactory MessageFactory { get; set; }

    private int _expectedMsgSeqNumLogOn;

    public FixMessageHandler(
        IFixBrokerageController brokerageController,
        ISecurityProvider securityProvider,
        RBISymbolMapper symbolMapper,
        string account
        )
    {
        _symbolMapper = symbolMapper;
        _securityProvider = securityProvider;
        _brokerageController = brokerageController;
        _account = account;
    }

    public bool AreSessionsReady()
    {
        return !_sessionHandlers.IsEmpty && 
               _sessionHandlers.All(kvp => Session.LookupSession(kvp.Key).IsLoggedOn);
    }

    public void Handle(Message message, SessionID sessionId)
    {
        if (!_sessionHandlers.TryGetValue(sessionId, out var handler))
        {
            Log.Trace($"Handle(): No controller was registered");
        }
        
        Crack(message, sessionId);
    }

    public void HandleAdminMessage(Message message, SessionID sessionId)
    {
        switch (message)
        {
            case Logout:
                HandleLogout(message);
                break;
            
            case Heartbeat:
                Log.Trace($"{message.GetType().Name}: {message} heartbeat");
                break;
        }
    }

    public void EnrichMessage(Message message)
    {
        if (message.IsSetField(MsgType.TAG))
        {
            var msgType = message.GetString(MsgType.TAG);

            if (!msgType.Equals(MsgType.REJECT) && !msgType.Equals(MsgType.BUSINESS_MESSAGE_REJECT))
            {
                switch (message)
                {
                    case Logon logon:
                        logon.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.NO));
                        logon.SetField(new EncryptMethod(EncryptMethod.NONE));
                        break;
                }
            }
        }
    }

    public void OnLogon(SessionID sessionId)
    {
        Log.Trace($"OnLogon(): Adding handler for SessionId {sessionId}");
        var session = new RBIFixConnection(sessionId);
        _sessionHandlers[sessionId] =
            new FixSymbolController(session, _brokerageController, _securityProvider, _symbolMapper, _account);
        if (_expectedMsgSeqNumLogOn > 0)
        {
            Session.LookupSession(sessionId).NextSenderMsgSeqNum = _expectedMsgSeqNumLogOn;
            _expectedMsgSeqNumLogOn = 0;
        }
    }

    public void OnLogout(SessionID sessionId)
    {
        if (_sessionHandlers.TryRemove(sessionId, out var handler))
        {
            _brokerageController.Unregister(handler);
        }
    }

    public void OnMessage(ExecutionReport report, SessionID sessionId)
    {
        Log.Trace($"OnMessage(ExecutionReport): {report}");

        var orderId = report.OrderID.getValue();
        var clOrdId = report.ClOrdID.getValue();
        var execType = report.ExecType.getValue();

        var orderStatus = Utility.ConvertOrderStatus(report);

        if (!clOrdId.IsNullOrEmpty())
        {
            if (orderStatus == OrderStatus.Invalid)
            {
                Log.Error($"Invalid order status: {report}");
            }
            else
            {
                Log.Trace($"ExecutionReport: Id = {orderId}, ClOrdId = {clOrdId}, ExecType = {execType}, Status = {orderStatus}");
            }

            _brokerageController.Receive(report);
        }
    }

    public void OnMessage(OrderCancelReject reject, SessionID sessionId)
    {
        var (reason, responseTo, text) = this.MapCancelReject(reject);

        Log.Trace($"OnMessage() : Order cancellation or modifying failed: {reason}, {text}, in response to {responseTo}");
    }

    private void HandleLogout(Message msg)
    {
        if (!msg.IsSetField(Text.TAG))
        {
            return;
        }

        var msgText = msg.GetString(Text.TAG);
        if (msgText.Contains("expected"))
        {
            var expected = Regex.Match(msgText, @"(?<=expected)[[0-9]+]").Value;
            expected = expected.Remove(0, 1);
            expected = expected.Remove(expected.Length - 1, 1);
            
            int.TryParse(expected, out _expectedMsgSeqNumLogOn);
        }
        else if(msgText.Contains("is closed") || msgText.Contains("Received"))
        {
            Log.Trace($"Logout, message: {msgText}");
        }
        else
        {
            msg.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.NO));
            _expectedMsgSeqNumLogOn = 0;
        }
    }

    private (string reason, string responseTo, string text) MapCancelReject(OrderCancelReject rejection)
    {
        try
        {
            var reason = rejection.CxlRejReason.getValue() switch
            {
                CxlRejReason.TOO_LATE_TO_CANCEL => "Too late to cancel",
                CxlRejReason.UNKNOWN_ORDER => "Unknown order",
                CxlRejReason.BROKER_OPTION => "Broker option",
                CxlRejReason.ORDER_ALREADY_IN_PENDING_CANCEL_OR_PENDING_REPLACE_STATUS =>
                    "Order already in Pending Cancel or Pending Replace status",
                _ => string.Empty
            };

            var responseTo = rejection.CxlRejResponseTo.getValue() switch
            {
                CxlRejResponseTo.ORDER_CANCEL_REQUEST => "Order cancel request",
                CxlRejResponseTo.ORDER_CANCEL_REPLACE_REQUEST => "Order cancel replace request",
                _ => string.Empty
            };

            var text = rejection.Text.getValue();

            return (reason, responseTo, text);
        }

        catch (Exception e)
        {
            Log.Trace($"Unexpected error {e.Message}");
            
            return (string.Empty, string.Empty, string.Empty);
        }
    }
}