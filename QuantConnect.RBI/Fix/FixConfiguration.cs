using QuickFix;

namespace QuantConnect.RBI.Fix;

public class FixConfiguration
{
    public string FixVersionString { get; set; } = "FIX.4.2";

    // market data session
    public string SenderCompId { get; set; } = "CLIENT1";
    public string TargetCompId { get; set; } = "SIMPLE";
    public string Host { get; set; } = "192.168.1.103";
    public long Port { get; set; } = 5080;

    public SessionSettings GetDefaultSessionSettings()
    {
        var settings = new SessionSettings();

        var defaultDic = new Dictionary();
        defaultDic.SetString("ConnectionType", "initiator");
        defaultDic.SetString("ReconnectInterval", "1");
        defaultDic.SetString("FileStorePath", @"store");
        defaultDic.SetString("FileLogPath", "log");
        defaultDic.SetString("StartTime", "00:00:00");
        defaultDic.SetString("EndTime", "00:00:00");
        defaultDic.SetBool("UseDataDictionary", true);
        defaultDic.SetString("DataDictionary", @"../../../QuantConnect.RBI/RBI-FIX42.xml");
        defaultDic.SetString("BeginString", FixVersionString);
        defaultDic.SetString("TimeZone", "UTC");
        defaultDic.SetBool("UseLocalTime", false);
        defaultDic.SetBool("SendLogoutBeforeDisconnectFromTimeout", false);
        defaultDic.SetString("HeartBtInt", "30");
        defaultDic.SetString("LogonTimeout", "15");

        settings.Set(defaultDic);

        var orderRoutingDic = new Dictionary();
        orderRoutingDic.SetString("SenderCompID", SenderCompId);
        orderRoutingDic.SetString("TargetCompID", TargetCompId);
        orderRoutingDic.SetString("SocketConnectHost", "127.0.0.1");
        orderRoutingDic.SetLong("SocketConnectPort", Port);

        var orderRoutingSessionId = new SessionID(FixVersionString, SenderCompId, TargetCompId);
        settings.Set(orderRoutingSessionId, orderRoutingDic);

        return settings;
    }
}