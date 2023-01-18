using QuickFix;

namespace QuantConnect.RBI.Fix.Core.Interfaces;

public interface IFixMessageHandler
{
    bool AreSessionsReady();
    
    IMessageFactory MessageFactory { get; set; }

    void Handle(Message message, SessionID sessionId);

    void EnrichMessage(Message message);

    void OnLogon(SessionID sessionId);

    void OnLogout(SessionID sessionId);
}