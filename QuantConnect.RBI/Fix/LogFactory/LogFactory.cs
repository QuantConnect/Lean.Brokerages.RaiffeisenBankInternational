using QuickFix;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.RBI.Fix.LogFactory;

public class LogFactory : ILogFactory
{
    private static readonly Dictionary<SessionID, ILog> Loggers = new();
    
    public ILog Create(SessionID sessionID)
    {
        if (Loggers.TryGetValue(sessionID, out var logger))
        {
            return logger;
        }
        
        Loggers.Add(sessionID, new Logger());
        return Loggers[sessionID];
    }
}