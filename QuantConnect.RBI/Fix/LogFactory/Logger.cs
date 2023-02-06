using QuickFix;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.RBI.Fix.LogFactory;

public class Logger : ILog
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        
    }

    public void OnIncoming(string msg)
    {
        if (CheckMessage(msg))
        {
            Log.Trace($"Incoming: {msg.Replace('\x1', '|')}", true);
        }
    }

    public void OnOutgoing(string msg)
    {
        if (CheckMessage(msg))
        {
            Log.Trace($"Outcoming: {msg.Replace('\x1', '|')}", true);
        }
    }

    public void OnEvent(string s)
    {
        Log.Trace($"[event] {s.Replace('\x1', '|')}", true);
    }

    private bool CheckMessage(string msg)
    {
        if (msg.Contains($"{'\x1'}35=0{'\x1'}"))
        {
            // exclude heartbeats
            return false;
        }
        
        if (msg.Contains($"{'\x1'}35=3{'\x1'}"))
        {
            // exclude session level reject
            return false;
        }
        
        if (msg.Contains($"{'\x1'}35=j{'\x1'}"))
        {
            // exclude business message reject
            return false;
        }

        return true;
    }
}