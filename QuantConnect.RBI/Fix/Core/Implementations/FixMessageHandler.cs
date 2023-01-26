using System;
using System.Collections.Concurrent;
using QLNet;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Implementations;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.TemplateBrokerage.Fix.Utils;
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
            Logging.Log.Trace($"Handle(): No controller was registered");
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
                _expectedMsgSeqNumLogOn = GetExpectedMsgSeqNum(message);
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
        }
        else
        {
            Log.Trace($"OnLogon(): SenderCompId or TargetCompId is invalid ");
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
    
    private int GetExpectedMsgSeqNum(Message msg)
    {
        if (!msg.IsSetField(Text.TAG))
            return 0;

        var textMsg = msg.GetString(Text.TAG);
        return textMsg.Contains("expected")
            ? Int32.Parse(System.Text.RegularExpressions.Regex.Match(textMsg, @"(?<=expected\s)[0-9]+").Value)
            : 0;
    }
}