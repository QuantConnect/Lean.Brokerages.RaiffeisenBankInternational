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
    private readonly string _onBehalfOfCompID;
    private readonly ConcurrentDictionary<SessionID, IFixSymbolController> _sessionHandlers = new();

    public IMessageFactory MessageFactory { get; set; } = new MessageFactory();

    public FixMessageHandler(
        IFixBrokerageController brokerageController,
        ISecurityProvider securityProvider,
        RBISymbolMapper symbolMapper,
        string account,
        string onBehalfOfCompID
        )
    {
        _symbolMapper = symbolMapper;
        _securityProvider = securityProvider;
        _brokerageController = brokerageController;
        _account = account;
        _onBehalfOfCompID = onBehalfOfCompID;
    }

    public bool AreSessionsReady()
    {
        return _sessionHandlers.IsEmpty ? false : _sessionHandlers.All(kvp => Session.LookupSession(kvp.Key).IsLoggedOn);
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
            case Heartbeat:
                Log.Trace($"{message.GetType().Name}: {message} heartbeat");
                break;
        }
    }

    public void EnrichMessage(Message message)
    {
        switch (message)
        {
            case Logon logon:
                logon.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.YES));
                logon.SetField(new EncryptMethod(EncryptMethod.NONE));
                break;
        }
    }

    public void OnLogon(SessionID sessionId)
    {
        Log.Trace($"FixMessageHandler.OnLogon(): Adding handler for SessionId {sessionId}");

        var session = new RBIFixConnection(sessionId);
        _sessionHandlers[sessionId] = new FixSymbolController(session, _brokerageController, _securityProvider, _symbolMapper, _account, _onBehalfOfCompID);
    }

    public void OnLogout(SessionID sessionId)
    {
        Log.Trace($"FixMessageHandler.OnLogout(): Removing handler for SessionId: {sessionId}");

        if (_sessionHandlers.TryRemove(sessionId, out var handler))
        {
            _brokerageController.Unregister(handler);
        }
    }

    public void OnMessage(BusinessMessageReject reject, SessionID sessionId)
    {
        Log.Error($"FixMessageHandler.OnMessage(BusinessMessageReject): {reject}");
    }

    public void OnMessage(ExecutionReport report, SessionID sessionId)
    {
        Log.Trace($"FixMessageHandler.OnMessage(ExecutionReport): {report}");

        var orderId = report.OrderID.getValue();
        var clOrdId = report.ClOrdID.getValue();
        var execType = report.ExecType.getValue();

        var orderStatus = Utility.ConvertOrderStatus(report);

        if (!clOrdId.IsNullOrEmpty())
        {
            if (orderStatus != OrderStatus.Invalid)
            {
                Log.Trace($"FixMessageHandler.OnMessage(): ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
            }
            else
            {
                Log.Error($"FixMessageHandler.OnMessage(): ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
            }
        }
        var isStatusRequest = report.IsSetExecTransType() && report.ExecTransType.getValue() == ExecTransType.STATUS;

        if (!isStatusRequest)
        {
            _brokerageController.Receive(report);
        }
    }

    public void OnMessage(OrderCancelReject reject, SessionID sessionId)
    {
        var (reason, responseTo, text) = this.MapCancelReject(reject);

        _brokerageController.Message($"Order cancellation failed: {reason}, {text}, in response to {responseTo}", Brokerages.BrokerageMessageType.Warning);
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

            var text = string.Empty;
            if(rejection.IsSetField(Text.TAG))
            {
                text = rejection.Text.getValue();
            }

            return (reason, responseTo, text);
        }

        catch (Exception e)
        {
            Log.Trace($"Unexpected error {e.Message}");
            
            return (string.Empty, string.Empty, string.Empty);
        }
    }
}