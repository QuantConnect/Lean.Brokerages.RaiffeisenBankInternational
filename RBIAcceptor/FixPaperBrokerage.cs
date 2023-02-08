using Microsoft.VisualBasic.CompilerServices;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using Message = QuickFix.Message;

namespace RBIAcceptor;

public class FixPaperBrokerage : MessageCracker, IApplication
{
    private int _orderID = 0;
    private int _execID = 0;
    private static readonly decimal DEFAULT_MARKET_PRICE = 10;

    public static FixPaperBrokerage Create()
    {
        var senderCompId = "EXECUTOR";
        var targetCompId = "CLIENT1";

        var settings = Utils.GetBaseSettings("acceptor");

        var orderRoutingDic = new Dictionary();
        orderRoutingDic.SetLong("SocketAcceptPort", 5080);
        var orderRoutingSessionId = new SessionID(settings.Get().GetString("BeginString"), senderCompId, targetCompId);
        settings.Set(orderRoutingSessionId, orderRoutingDic);

        var gateway = new FixPaperBrokerage();
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new FileLogFactory(settings);
        var acceptor = new ThreadedSocketAcceptor(gateway, storeFactory, settings, logFactory);
        acceptor.Start();

        return gateway;
    }

    public void FromAdmin(Message message, SessionID sessionID)
    {
        Console.WriteLine($"FixGatewayBrokerage.FromAdmin({sessionID}): {message}");
    }

    public void FromApp(Message message, SessionID sessionID)
    {
        Console.WriteLine($"FixGatewayBrokerage.FromApp({sessionID}): {message}");
        var msgType = message.Header.GetString(Tags.MsgType);
        if (msgType == MsgType.ORDER_SINGLE)
        {
            OnMessage((NewOrderSingle) message, sessionID);
        }
    }

    public void OnCreate(SessionID sessionID)
    {
        Console.WriteLine($"FixGatewayBrokerage.OnCreate({sessionID}):");
    }

    public void OnLogon(SessionID sessionID)
    {
        Console.WriteLine($"FixGatewayBrokerage.OnLogon({sessionID}): added new client {sessionID.SessionQualifier}");
    }

    public void OnLogout(SessionID sessionID)
    {
        Console.WriteLine($"FixGatewayBrokerage.OnLogout({sessionID}): removed client {sessionID.SessionQualifier}");
    }

    public void ToAdmin(Message message, SessionID sessionID)
    {
        var msgType = message.Header.GetString(Tags.MsgType);
        var extraLog = string.Empty;
        if (msgType == MsgType.HEARTBEAT)
        {
            extraLog = "HeartBeat ";
        }

        Console.WriteLine($"FixGatewayBrokerage.ToAdmin({sessionID}): {extraLog}{message}");
    }

    public void ToApp(Message message, SessionID sessionID)
    {
        var msgType = message.Header.GetString(Tags.MsgType);
        var extraLog = string.Empty;
        if (msgType == MsgType.HEARTBEAT)
        {
            extraLog = "HeartBeat ";
        }

        Console.WriteLine($"FixGatewayBrokerage.ToApp({sessionID}): {extraLog}{message}");
    }
    public void OnMessage(NewOrderSingle n, SessionID s)
    {
        Symbol symbol = n.Symbol;
        Side side = n.Side;
        OrdType ordType = n.OrdType;
        OrderQty orderQty = n.OrderQty;
        ClOrdID clOrdID = n.ClOrdID;
        Price price = new Price(DEFAULT_MARKET_PRICE);

        switch (ordType.getValue())
        {
            case OrdType.LIMIT:
                price = n.Price;
                if (price.Obj == 0)
                    throw new IncorrectTagValue(price.Tag);
                break;
            case OrdType.MARKET: break;
            default: throw new IncorrectTagValue(ordType.Tag);
        }

        ExecutionReport exReport = new ExecutionReport(
            new OrderID(GenOrderID()),
            new ExecID(GenExecID()),
            new ExecTransType(ExecTransType.NEW),
            new ExecType(ExecType.FILL),
            new OrdStatus(OrdStatus.FILLED),
            symbol,
            side,
            new LeavesQty(0),
            new CumQty(orderQty.getValue()),
            new AvgPx(price.getValue()));

        exReport.Set(clOrdID);
        exReport.Set(orderQty);
        exReport.Set(new LastShares(orderQty.getValue()));
        exReport.Set(new LastPx(price.getValue()));

        if (n.IsSetAccount())
            exReport.SetField(n.Account);

        try
        {
            Session.SendToTarget(exReport, s);
        }
        catch (SessionNotFound ex)
        {
            Console.WriteLine("==session not found exception!==");
            Console.WriteLine(ex.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public void OnMessage(OrderCancelReplaceRequest request, SessionID s)
    {
        string orderid = (request.IsSetOrderID()) ? request.OrderID.Obj : "unknown orderID";
        var executionReport = new ExecutionReport
        {
            OrdStatus = new OrdStatus('5'),
            OrderID = new OrderID(orderid),
            ClOrdID = request.ClOrdID,
            OrigClOrdID = request.OrigClOrdID,
            ExecType = new ExecType('5'),
            TransactTime = new TransactTime(DateTime.UtcNow)
        };
        
        try
        {
            Session.SendToTarget(executionReport, s);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public void OnMessage(OrderCancelRequest request, SessionID s)
    {
        string orderid = (request.IsSetOrderID()) ? request.OrderID.Obj : "unknown orderID";
        var executionReport = new ExecutionReport
        {
            OrdStatus = new OrdStatus('4'),
            OrderID = new OrderID(orderid),
            ClOrdID = request.ClOrdID,
            OrigClOrdID = request.OrigClOrdID,
            ExecType = new ExecType('4'),
            TransactTime = new TransactTime(DateTime.UtcNow)
        };
        
        try
        {
            Session.SendToTarget(executionReport, s);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private string GenOrderID()
    {
        return (++_orderID).ToString();
    }

    private string GenExecID()
    {
        return (++_execID).ToString();
    }
}