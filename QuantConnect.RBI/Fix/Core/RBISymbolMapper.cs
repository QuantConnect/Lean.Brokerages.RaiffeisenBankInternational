using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Brokerages;

namespace QuantConnect.RBI.Fix.Core;

public class RBISymbolMapper : ISymbolMapper
{
    private readonly Dictionary<string, SecurityType> _mapSecurityTypeToLeanSecurityType = new ()
    {
        { QuickFix.Fields.SecurityType.COMMON_STOCK, SecurityType.Equity },
        { QuickFix.Fields.SecurityType.FUTURE, SecurityType.Future },
        { QuickFix.Fields.SecurityType.OPTION, SecurityType.Option }
    };
    
    private readonly Dictionary<SecurityType, string> _mapLeanSecurityTypeToSecurityType;

    public RBISymbolMapper()
    {
        _mapLeanSecurityTypeToSecurityType = _mapSecurityTypeToLeanSecurityType
            .ToDictionary(x => x.Value, x => x.Key);
    }
    
    public string GetBrokerageSymbol(Symbol symbol)
    {
        return symbol.ID.Symbol;
    }

    public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market,
        DateTime expirationDate = new (), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
    {
        throw new NotImplementedException();
    }
    
    public SecurityType GetLeanSecurityType(string productType)
    {
        if (!_mapSecurityTypeToLeanSecurityType.TryGetValue(productType, out var securityType))
        {
            throw new NotSupportedException($"Unsupported TT ProductType: {productType}");
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
}
