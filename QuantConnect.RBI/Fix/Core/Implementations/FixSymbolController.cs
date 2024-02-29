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
using QuantConnect.Brokerages.RBI.Fix.Connection.Interfaces;
using QuantConnect.Brokerages.RBI.Fix.Core.Interfaces;
using QuantConnect.Brokerages.RBI.Fix.Utils;
using QuantConnect.Securities;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.Brokerages.RBI.Fix.Core.Implementations;

public class FixSymbolController : IFixSymbolController
{
    private readonly IRBIFixConnection _session;
    private readonly RBISymbolMapper _symbolMapper;
    private readonly Account _account;
    private readonly ClientID _clientID;

    public FixSymbolController(
        IRBIFixConnection session,
        IFixBrokerageController brokerageController,
        ISecurityProvider securityProvider,
        RBISymbolMapper mapper,
        string account,
        string onBehalfOfCompID
        )
    {
        _session = session;
        _symbolMapper = mapper;
        _account = new Account(account);
        brokerageController.Register(this);
        _clientID = new ClientID(onBehalfOfCompID);
    }

    public bool PlaceOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);
        var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);
        var securityType = new QuickFix.Fields.SecurityType(_symbolMapper.GetBrokerageSecurityType(order.Symbol.SecurityType));

        /// Raiffeisen Centrobank AG identifies instruments using ISIN codes. Therefore IDSource (22) needs to contain the class ISINCode (4)
        /// and SecurityID (48) needs to contain the ISIN. The field Symbol(55) has to be filled but will not be validated.
        var newOrder = new NewOrderSingle
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            SecurityType = securityType,
            Side = side,
            TransactTime = new TransactTime(DateTime.UtcNow),
            OrderQty = new OrderQty(order.AbsoluteQuantity),
            IDSource = new IDSource(IDSource.ISIN_NUMBER),
            SecurityID = new SecurityID(order.Symbol.ISIN),
            TimeInForce = Utility.ConvertTimeInForce(order.TimeInForce, order.Type),
            // US
            ExDestination = new ExDestination(order.Symbol.ISIN.Substring(0, 2)),
            Account = _account,
            Currency = new Currency(order.PriceCurrency),
            ClientID = _clientID
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
                Log.Error($"FixSymbolController.PlaceOrder(): RBI doesn't support this OrderType: {nameof(order.Type)}");
                break;
        }
        order.BrokerId.Add(newOrder.ClOrdID.getValue());
        
        return _session.Send(newOrder);
    }

    public bool CancelOrder(Order order)
    {
        var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);

        var request = new OrderCancelRequest
        {
            ClOrdID = new ClOrdID(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
            OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
            Side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL),
            TransactTime = new TransactTime(DateTime.UtcNow),
            Symbol = new QuickFix.Fields.Symbol(ticker),
            OrderID = new OrderID(order.Id.ToString()),
            Account = _account,
            ClientID = _clientID
        };
        order.BrokerId.Add(request.ClOrdID.getValue());

        return _session.Send(request);
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
            OrderID = new OrderID(order.Id.ToString()),
            Account = _account,
            Currency = new Currency(order.PriceCurrency),
            ClientID = _clientID
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
        order.BrokerId.Add(request.ClOrdID.getValue());

        return _session.Send(request);
    }
}