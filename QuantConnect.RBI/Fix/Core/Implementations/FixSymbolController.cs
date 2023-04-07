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

using System.Globalization;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.RBI.Fix.Connection.Interfaces;
using QuantConnect.RBI.Fix.Core.Interfaces;
using QuantConnect.RBI.Fix.Utils;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.RBI.Fix.Core.Implementations;

public class FixSymbolController : IFixSymbolController
{
    private readonly IRBIFixConnection _session;
    private readonly RBISymbolMapper _symbolMapper;
    private readonly ISecurityProvider _securityProvider;

    public FixSymbolController(
        IRBIFixConnection session,
        IFixBrokerageController brokerageController,
        ISecurityProvider securityProvider,
        RBISymbolMapper mapper
        )
    {
        _session = session;
        _symbolMapper = mapper;
        _securityProvider = securityProvider;
        brokerageController.Register(this);
    }

    public bool PlaceOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);
        var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);
        var securityId =
            SecurityIdentifier.GenerateEquity(ticker, Market.USA, mappingResolveDate: DateTime.UtcNow);

        var newOrder = new NewOrderSingle
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            Side = side,
            TransactTime = new TransactTime(DateTime.UtcNow),
            OrderQty = new OrderQty(order.AbsoluteQuantity),
            IDSource = new IDSource(IDSource.ISIN_NUMBER),
            SecurityID = new SecurityID(securityId.ToString()),
            TimeInForce = Utility.ConvertTimeInForce(order.TimeInForce, order.Type),
            ExDestination = new ExDestination(GetOrderExchange(order))
        };

        switch (order.Type)
        {
            case OrderType.Limit:
                newOrder.OrdType = new OrdType(OrdType.LIMIT);
                newOrder.Price = new Price(((LimitOrder) order).LimitPrice);
                break;
            
            case OrderType.Market:
                newOrder.OrdType = new OrdType(OrdType.MARKET);
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

            default:
                Log.Trace($"RBI doesn't support this OrderType: {nameof(order.Type)}");
                break;
        }
        
        order.BrokerId.Add(newOrder.ClOrdID.getValue());
        
        return _session.Send(newOrder);
    }

    public bool CancelOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);

        return _session.Send(new OrderCancelRequest
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
            Side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL),
            TransactTime = new TransactTime(DateTime.UtcNow),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            OrderID = new OrderID(order.Id.ToString()),
        });
    }

    public bool UpdateOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);
        
        var request = new OrderCancelReplaceRequest
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
            TransactTime = new TransactTime(DateTime.UtcNow),
            OrderQty = new OrderQty(order.Quantity),
            Side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
            OrderID = new OrderID(order.Id.ToString())
        };

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

            default:
                Log.Trace($"RBI doesn't support this Order Type: {nameof(order.Type)}");
                break;
        }

        return _session.Send(request);
    }
    
    private string GetOrderExchange(Order order)
    {
        var exchangeDestination = string.Empty;
        if (order.Properties is OrderProperties orderProperties && orderProperties.Exchange != null)
        {
            exchangeDestination = orderProperties.Exchange.ToString();
        }
        if (string.IsNullOrEmpty(exchangeDestination) && order.Symbol.SecurityType == SecurityType.Equity)
        {
            var equity = _securityProvider.GetSecurity(order.Symbol) as Equity;
            exchangeDestination = equity?.PrimaryExchange.ToString();
        }

        var exchangeFromMapper = _symbolMapper.GetBrokerageExchange(exchangeDestination.ToUpper());

        if (!exchangeFromMapper.isSuccessful)
        {
            exchangeFromMapper.exchange = "SMART-INCA"; // change
        }

        return exchangeFromMapper.exchange;
    }
}