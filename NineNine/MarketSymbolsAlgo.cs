using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Securities;
using System;

namespace NineNine {

    /// <summary>
    // List symbols for market.
    /// </summary>
    public class MarketSymbolsAlgo : QCAlgorithm {

        private readonly string _market = Market.Oanda;
        private readonly SecurityType _securityType = SecurityType.Cfd;
        

        /// <summary>
        /// Initialize algorithm.
        /// </summary>
        public override void Initialize() {
            Debug($"Algorithm Mode: {AlgorithmMode}. Deployment Target: {DeploymentTarget}. TimeZone: {TimeZone.Id}");

            SetCash(100000);
            SetStartDate(DateTime.Now.AddDays(-1).Date);
            SetEndDate(DateTime.Now.Date);

            // get all symbols for market/security type
            var props = SymbolPropertiesDatabase.GetSymbolPropertiesList(_market);
            foreach (var prop in props) {
                var symbol = QuantConnect.Symbol.Create(prop.Key.Symbol, prop.Key.SecurityType, prop.Key.Market);
                Debug($"{symbol.Value} ({prop.Value.Description}): {prop.Key}");
            }

        }

    }
}
