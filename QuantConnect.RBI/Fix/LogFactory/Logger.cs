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

using QuickFix;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.RBI.Fix.LogFactory;

public class Logger : ILog
{
    private readonly bool _logFixMessages;

    public Logger(bool logFixMesssages)
    {
        _logFixMessages = logFixMesssages;
    }

    public void Dispose()
    {
    }

    public void Clear()
    { }

    public void OnIncoming(string msg)
    {
        if (_logFixMessages && CheckMessage(msg))
        {
            Log.Trace($"[incoming] {msg.Replace('\x1', '|')}", true);
        }
    }

    public void OnOutgoing(string msg)
    {
        if (_logFixMessages && CheckMessage(msg))
        {
            Log.Trace($"[outgoing] {msg.Replace('\x1', '|')}", true);
        }
    }

    public void OnEvent(string s)
    {
        if(_logFixMessages)
        {
            Log.Trace($"[   event] {s.Replace('\x1', '|')}", true);
        }
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