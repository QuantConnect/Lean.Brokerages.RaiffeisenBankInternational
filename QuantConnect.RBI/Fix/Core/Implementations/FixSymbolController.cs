using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Tests;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixSymbolController : IFixSymbolController
{
    private readonly IRBIFixConnection _session;
    private readonly RBISymbolMapper _symbolMapper;

    public FixSymbolController(IRBIFixConnection session)
    {
        _session = session;
        _symbolMapper = new RBISymbolMapper();
    }

    public bool SubscribeToSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
    }

    public bool UnsubscribeFromSymbol(Symbol symbol)
    {
        throw new System.NotImplementedException();
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
            OrderQty = new OrderQty(order.Quantity),
            SecurityType = securityType,
            IDSource = new IDSource("4"),
            // change to ISINCode
            SecurityID = new SecurityID(securityId.ToString()),
            //add timeinforce
            //add ex destination
            Currency = new Currency(order.PriceCurrency)
        };


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
            
            // add market on limit
            default:
                Logging.Log.Trace($"RBI doesn't support this Order Type: {nameof(order.Type)}");
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
            OrderQty = new OrderQty(order.Quantity)
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

            // add market on limit
            default:
                Log.Trace($"RBI doesn't support this Order Type: {nameof(order.Type)}");
                break;
        }

        return _session.Send(request);
    }
    
    // private IEnumerable<Securities.SecurityDefinition> GetSecurityDefinitions()
    // {
    //     var _securitiesDefinitionKey = Path.Combine(Globals.DataFolder, "symbol-properties", "security-database.csv");
    //     
    //     if (!Securities.SecurityDefinition.TryRead(_dataProvider, _securitiesDefinitionKey, out var securityDefinitions))
    //     {
    //         securityDefinitions = new List<Securities.SecurityDefinition>();
    //         Log.Error($"SecurityDefinitionSymbolResolver(): No security definitions data loaded from file: {_securitiesDefinitionKey}");
    //     }
    //     return securityDefinitions;
    // }
}