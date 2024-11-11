using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;

namespace NineNine {

    /// <summary>
    // Algo with universe that selects a new security every day.
    /// </summary>
    public class UniverseAlgo : QCAlgorithm {

        private Universe _universe;
        
        /// <summary>
        /// Initialize algorithm.
        /// </summary>
        public override void Initialize() {

            SetTimeZone("Europe/Stockholm");
            SetStartDate(2013, 10, 04);
            SetEndDate(2013, 10, 11);

            UniverseSettings.Resolution = Resolution.Daily;
            _universe = AddUniverse(SymbolFilter);

        }


        /// <summary>
        /// Runs on start of algo and then every day (midnight in the New York time zone).
        /// </summary>
        /// <param name="fundamental"></param>
        /// <returns></returns>
        private IEnumerable<Symbol> SymbolFilter(IEnumerable<CoarseFundamental> fundamental) {

            // filter out the most liquid equities
            var top = fundamental.Where(x => x.HasFundamentalData)
                .OrderByDescending(x => x.DollarVolume)
                .Select(x => x.Symbol)
                .Take(20);


            if (!top.Any()) {
                var tickers = new[] { "AIG", "BAC", "IBM", "SPY" };
                top = tickers.Select(x => QuantConnect.Symbol.Create(x, SecurityType.Equity, Market.USA));
            }

            // randomly select 1
            var arr = top.ToArray();
            var symbol = arr.ToArray()[Random.Shared.Next(arr.Length)];

            Debug($"Symbol: {symbol.Value} selected to universe ({Time.DayOfWeek} {Time.ToShortTimeString()})");


            

            return new[] { symbol };
        }

        /// <summary>
        /// OnData event is the primary entry point for the algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice) {
            foreach (var kv in slice) {
                var symbol = kv.Key;
                Debug($"Symbol: {symbol.Value} Price: { kv.Value.Price}");

                if (Securities.TryGetValue(symbol, out var security)) {
                    if (security.Invested) {
                        //Debug($"Time: {Time} Symbol: {symbol} invested");

                        if (Time.Hour == 18 && security.Symbol.Value == "SPY") {
                            Debug($"Symbol: {symbol.Value} closing position ({Time.DayOfWeek} {Time.ToShortTimeString()})");
                            security["closed"] = Time;
                            //RemoveSecurity(symbol);
                            Liquidate(symbol);
                        }

                    } else {
                        if (security.TryGet<DateTime>("closed", out var closed) && closed.Date == Time.Date) {
                            //Debug($"Symbol: {symbol.Value} closed for today ({Time.DayOfWeek} {Time.ToShortTimeString()})");
                        } else {
                            if (security.IsTradable) {
                                Debug($"Symbol: {symbol.Value} Opening position ({Time.DayOfWeek} {Time.ToShortTimeString()})");
                                SetHoldings(symbol, 0.1);
                            }
                        }
                    }
                } else {
                    Debug($"Time: {Time} Symbol: {symbol} Not found");
                }
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes) {

            foreach (var security in changes.AddedSecurities) {
                Debug($"Symbol: {security.Symbol.Value} added to universe ({Time.DayOfWeek} {Time.ToShortTimeString()})");
            }

            foreach (var security in changes.RemovedSecurities) {
                Debug($"Symbol: {security.Symbol.Value} removed from universe ({Time.DayOfWeek} {Time.ToShortTimeString()})");
            }
            
            Debug($"Universe.Selected: {string.Join(",", _universe.Selected.Select(x => x.Value))}");
            Debug($"ActiveSecurities: {string.Join(",", ActiveSecurities.Keys.Select(x => x.Value))}");
            Debug($"Portfolio.Invested: {string.Join(",", Portfolio.Where(x => x.Value.Invested).Select(x => x.Key.Value))}");
            //Debug($"Universe.Securities: {string.Join(",", _universe.Securities.Select(x => x.Key.Value))}");
            //Debug($"Universe.Members: {string.Join(",", _universe.Members.Select(x => x.Key.Value))}");
        }

        public override void OnEndOfDay(Symbol symbol) {
            //Debug($"Symbol: {symbol.Value} end of day ({Time.DayOfWeek} {Time.ToShortTimeString()})");
        }

    }
}
