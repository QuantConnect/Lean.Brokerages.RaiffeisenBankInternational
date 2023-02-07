using QuickFix;
namespace RBIAcceptor;
public static class Utils
{
    public static SessionSettings GetBaseSettings(string connectionType)
    {
        var fileName = "RBI-FIX42.xml";
        if (!File.Exists(fileName))
        {
            throw new ArgumentException($"Failed to find required configuration file {fileName}");
        }


        var settings = new SessionSettings();

        var defaultDic = new Dictionary();
        defaultDic.SetString("ConnectionType", connectionType);
        defaultDic.SetString("ReconnectInterval", "5");
        defaultDic.SetString("FileStorePath", @"store");
        defaultDic.SetString("FileLogPath", "log");
        defaultDic.SetString("StartTime", "00:00:00");
        defaultDic.SetString("EndTime", "00:00:00");
        defaultDic.SetBool("UseDataDictionary", true);
        defaultDic.SetString("DataDictionary", fileName);
        defaultDic.SetString("BeginString", "FIX.4.2");
        defaultDic.SetString("TimeZone", "UTC");
        defaultDic.SetBool("UseLocalTime", false);
        defaultDic.SetBool("SendLogoutBeforeDisconnectFromTimeout", false);
        defaultDic.SetString("HeartBtInt", "30");
        defaultDic.SetString("LogonTimeout", "15");
        settings.Set(defaultDic);

        return settings;
    }
}