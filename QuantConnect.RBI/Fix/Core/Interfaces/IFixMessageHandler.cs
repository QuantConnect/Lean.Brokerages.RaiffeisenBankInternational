using QuickFix;
using QuickFix.FIX42;
using Message = QuickFix.Message;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixMessageHandler
{
    bool IsSessionReady();
    
    IMessageFactory MessageFactory { get; set; }

    void Handle(Message message, SessionID sessionId);

    void HandleAdminMessage(Message message, SessionID sessionId);

    void EnrichMessage(Message message);

    void OnLogon(SessionID sessionId);

    void OnLogout(SessionID sessionId);

    void OnMessage(ExecutionReport report, SessionID sessionId);

    void OnMessage(OrderCancelReject reject, SessionID sessionId);
}