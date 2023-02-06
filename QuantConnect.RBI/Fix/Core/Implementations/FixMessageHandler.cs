using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using QLNet;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Implementations;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuantConnect.Util;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using Log = QuantConnect.Logging.Log;
using Message = QuickFix.Message;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixMessageHandler : MessageCracker, IFixMessageHandler
{
    private readonly FixConfiguration _config;
    private readonly IFixBrokerageController _brokerageController;
    private IFixSymbolController _fixSymbolController;
    
    private int _expectedMsgSeqNumLogOn = default;
    public bool IsReady { get; set; }

    public FixMessageHandler(FixConfiguration config, IFixBrokerageController brokerageController)
    {
        _config = config;
        _brokerageController = brokerageController;
    }

    public bool IsSessionReady()
    {
        return IsReady;
    }

    public IMessageFactory MessageFactory { get; set; }
    
    public void Handle(Message message, SessionID sessionId)
    {
        if (_brokerageController == null)
        {
            Log.Trace($"Handle(): No controller was registered");
        }
        
        Crack(message, sessionId);
    }
    
    public void OnRecoveryCompleted()
    {
        IsReady = true;
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
        switch (message)
        {
            case Logon logon:
                logon.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.NO));
                if (_expectedMsgSeqNumLogOn > 0)
                {
                    logon.SetField(new MsgSeqNum(_expectedMsgSeqNumLogOn));
                }
                break;
            
            case SequenceReset reset:
                reset.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.YES));
                break;
            
        }
    }

    public void OnLogon(SessionID sessionId)
    {
        Log.Trace($"OnLogon(): Adding handler for SessionId {sessionId}");

        if (sessionId.SenderCompID == _config.SenderCompId && sessionId.TargetCompID == _config.TargetCompId)
        {
            _fixSymbolController = new FixSymbolController(new RBIFixConnection(sessionId));
            _brokerageController.Register(_fixSymbolController);
            if (_expectedMsgSeqNumLogOn > 0)
            {
                _expectedMsgSeqNumLogOn = 0;
            }
        }
        else
        {
            Log.Trace("OnLogon(): SenderCompId or TargetCompId is invalid ");
        }
    }

    public void OnLogout(SessionID sessionId)
    {
        if (sessionId.SenderCompID == _config.SenderCompId && sessionId.TargetCompID == _config.TargetCompId)
        {
            _brokerageController.Unregister(_fixSymbolController);
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