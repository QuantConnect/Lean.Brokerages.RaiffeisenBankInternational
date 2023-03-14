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

using QuantConnect.Brokerages;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;

namespace QuantConnect.RBI;

public class RBISymbolMapper : ISymbolMapper
{
    private readonly IMapFileProvider _mapFileProvider;
    
    private readonly Dictionary<string, SecurityType> _mapSecurityTypeToLeanSecurityType = new ()
    {
        { QuickFix.Fields.SecurityType.COMMON_STOCK, SecurityType.Equity },
        { QuickFix.Fields.SecurityType.FUTURE, SecurityType.Future },
        { QuickFix.Fields.SecurityType.OPTION, SecurityType.Option }
    };

    private readonly Dictionary<string, string> _mapLeanExchangeToExchange = new() {
        { Exchange.AMEX.Name, "AMEX-INCA" },
        { Exchange.BOSTON.Name, "BOSTON-INCA" },
        { Exchange.CBOE.Name, "CBOE-INCA" },
        { Exchange.ARCA.Name, "ARCA-INCA" },
        { Exchange.BATS.Name, "BATS-INCA" },
        { Exchange.BATS_Y.Name, "BATSYX-INCA" },
        { Exchange.EDGA.Name, "EDGA-INCA" },
        { Exchange.EDGX.Name, "EDGX-INCA" },
        { Exchange.NASDAQ.Name, "NASDAQ-INCA" },
        { Exchange.NASDAQ_BX.Name, "NASDAQBX-INCA" },
        { Exchange.NYSE.Name, "NYSE-INCA" },
        { Exchange.NASDAQ_PSX.Name, "PHLX-INCA" },
        { "SMART", "SMART-INCA" },
        { "IEX", "IEX-INCA" },
        { "OTCX", "OTCX-INCA" },
    };
    
    private readonly Dictionary<SecurityType, string> _mapLeanSecurityTypeToSecurityType;

    public RBISymbolMapper(IMapFileProvider mapFileProvider)
    {
        _mapFileProvider = mapFileProvider;
        _mapLeanSecurityTypeToSecurityType = _mapSecurityTypeToLeanSecurityType
            .ToDictionary(x => x.Value, x => x.Key);
    }
    
    public string GetBrokerageSymbol(Symbol symbol)
    {
        return GetMappedTicker(symbol);
    }

    public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market,
        DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
    {
        throw new NotImplementedException();
    }

    public SecurityType GetLeanSecurityType(string productType)
    {
        if (!_mapSecurityTypeToLeanSecurityType.TryGetValue(productType, out var securityType))
        {
            throw new NotSupportedException($"Unsupported RBI ProductType: {productType}");
        }

        return securityType;
    }
    
    public string GetBrokerageSecurityType(SecurityType leanSecurityType)
    {
        if (!_mapLeanSecurityTypeToSecurityType.TryGetValue(leanSecurityType, out var securityTypeBrokerage))
        {
            throw new NotSupportedException($"Unsupported LEAN security type: {leanSecurityType}");
        }

        return securityTypeBrokerage;
    }

    public (bool isSuccessful, string? exchange) GetExchange(string leanExchange)
    {
        return (_mapLeanExchangeToExchange.TryGetValue(leanExchange, out var exchange), exchange);
    }
    
    private string GetMappedTicker(Symbol symbol)
    {
        var ticker = symbol.ID.Symbol;
        if (symbol.ID.SecurityType == SecurityType.Equity)
        {
            var mapFile = _mapFileProvider.Get(AuxiliaryDataKey.Create(symbol)).ResolveMapFile(symbol);
            ticker = mapFile.GetMappedSymbol(DateTime.UtcNow, symbol.Value);
        }

        return ticker;
    }
}
