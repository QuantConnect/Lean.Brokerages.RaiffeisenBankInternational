using QuickFix;

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
}