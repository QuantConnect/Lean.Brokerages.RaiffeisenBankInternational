using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuantConnect.Securities;
using QuickFix.Fields;
using QuickFix.FIX42;
using TimeInForce = QuickFix.Fields.TimeInForce;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixSymbolController : IFixSymbolController
{
    private readonly IRBIFixConnection _session;
    private readonly RBISymbolMapper _symbolMapper;
    private readonly IFixBrokerageController _brokerageController;

    public FixSymbolController(IRBIFixConnection session, IFixBrokerageController brokerageController)
    {
        _session = session;
        _symbolMapper = new RBISymbolMapper();
        _brokerageController = brokerageController;
        _brokerageController.Register(this);
    }

    public bool PlaceOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);

        var securityType = new QuickFix.Fields.SecurityType(_symbolMapper.GetBrokerageSecurityType(order.Symbol.SecurityType));

        var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);
        
        var securityId =
            SecurityIdentifier.GenerateEquity(ticker, Market.USA, mappingResolveDate: DateTime.UtcNow);

        var newOrder = new NewOrderSingle()
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            Side = side,
            TransactTime = new TransactTime(DateTime.UtcNow),
            OrderQty = new OrderQty(order.AbsoluteQuantity),
            SecurityType = securityType,
            IDSource = new IDSource(IDSource.ISIN_NUMBER),
            // change to ISINCode
            SecurityID = new SecurityID(securityId.ToString()),
            Currency = new Currency(order.PriceCurrency),
            TimeInForce = Utility.ConvertTimeInForce(order.TimeInForce, order.Type),
        };

        var orderProperties = order.Properties as OrderProperties;

        newOrder.ExDestination = new ExDestination(orderProperties?.Exchange?.ToString() ?? string.Empty);

        switch (order.Type)
        {
            case OrderType.Limit:
                newOrder.OrdType = new OrdType(OrdType.LIMIT);
                newOrder.Price = new Price(((LimitOrder) order).LimitPrice);
                break;
            
            case OrderType.Market:
                newOrder.OrdType = new OrdType(OrdType.MARKET);
                newOrder.Price = new Price(((MarketOrder)order).Price);
                break;
            
            case OrderType.StopLimit:
                newOrder.OrdType = new OrdType(OrdType.STOP_LIMIT);
                newOrder.Price = new Price(((StopLimitOrder) order).LimitPrice);
                newOrder.StopPx = new StopPx(((StopLimitOrder) order).StopPrice);
                break;
            
            case OrderType.StopMarket:
                newOrder.OrdType = new OrdType(OrdType.STOP);
                newOrder.StopPx = new StopPx(((StopMarketOrder) order).StopPrice);
                break;
            // if this is correct - uncomment
            // case OrderType.LimitIfTouched:
            //     newOrder.OrdType = new OrdType(OrdType.MARKET_IF_TOUCHED);
            //     newOrder.Price = new Price(((LimitIfTouchedOrder) order).LimitPrice);
            //     newOrder.StopPx = new StopPx(((LimitIfTouchedOrder) order).TriggerPrice);
            //     break;
            
            default:
                Log.Trace($"RBI doesn't support this Order Type: {nameof(order.Type)}");
                break;
        }

        Log.Trace($"FixSymbolController.PlaceOrder(): sending order {order.Id}...");
        order.BrokerId.Add(newOrder.ClOrdID.getValue());
        
        return _session.Send(newOrder);
    }

    public bool CancelOrder(Order order)
    {
        return _session.Send(new OrderCancelRequest
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            OrigClOrdID = new OrigClOrdID(order.BrokerId[0])
        });
    }

    public bool UpdateOrder(Order order)
    {
        var request = new OrderCancelReplaceRequest
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
            Symbol = new QuickFix.Fields.Symbol(order.Symbol.Value),
            TransactTime = new TransactTime(order.Time),
            OrderQty = new OrderQty(order.Quantity),
        };

        var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);
        request.Side = side;

        switch (order.Type)
        {
            case OrderType.Limit:
                request.OrdType = new OrdType(OrdType.LIMIT);
                request.Price = new Price(((LimitOrder) order).LimitPrice);
                break;

            case OrderType.Market:
                request.OrdType = new OrdType(OrdType.MARKET);
                break;

            case OrderType.StopLimit:
                request.OrdType = new OrdType(OrdType.STOP_LIMIT);
                request.Price = new Price(((StopLimitOrder) order).LimitPrice);
                request.StopPx = new StopPx(((StopLimitOrder) order).StopPrice);
                break;

            case OrderType.StopMarket:
                request.OrdType = new OrdType(OrdType.STOP);
                request.StopPx = new StopPx(((StopMarketOrder) order).StopPrice);
                break;

            // if this is correct - uncomment
            // case OrderType.LimitIfTouched:
            //     request.OrdType = new OrdType(OrdType.MARKET_IF_TOUCHED);
            //     request.Price = new Price(((LimitIfTouchedOrder) order).LimitPrice);
            //     request.StopPx = new StopPx(((LimitIfTouchedOrder) order).TriggerPrice);
            //     break;
            
            default:
                Log.Trace($"RBI doesn't support this Order Type: {nameof(order.Type)}");
                break;
        }

        return _session.Send(request);
    }
}