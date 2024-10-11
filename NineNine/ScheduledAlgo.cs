using NodaTime;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Securities;
using System;
using System.Text;

namespace NineNine {

    /// <summary>
    // Algo that schedules events after market open and before market close.
    /// </summary>
    public class ScheduledAlgo : QCAlgorithm {


        // selected symbol
        private Symbol _symbol;

        /// <summary>
        /// Initialize algorithm.
        /// </summary>
        public override void Initialize() {

            SetTimeZone("Europe/Stockholm");
            SetStartDate(2013,10, 04);
            SetEndDate(2013, 10, 11);

            Debug($"Algorithm Mode: {AlgorithmMode}. Deployment Target: {DeploymentTarget}. TimeZone: {TimeZone.Id}");

            var security = AddEquity("SPY");
            _symbol = security.Symbol;

            Debug($"Algo time: {Time} ({TimeZone})");
            Debug($"{_symbol.Value} time: {security.LocalTime} ({security.Exchange.TimeZone})");

            var hours = security.Exchange.Hours;
            //hours = MarketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);

            var sb = new StringBuilder();
            sb.AppendLine($"{_symbol.Value} market hours ({hours.TimeZone}):");
            foreach (var day in hours.MarketHours.Values) {
                if (day.IsOpenAllDay || day.IsClosedAllDay) {
                    sb.AppendLine($"  {day.DayOfWeek}: {day}");
                } else {
                    sb.AppendLine($"  {day}");
                }
            }
            Debug(sb.ToString());

            Schedule.On(DateRules.EveryDay(), TimeRules.AfterMarketOpen(_symbol), LogOpen);
        }

        /// <summary>
        /// OnData event is the primary entry point for the algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice) {
            //Debug($"Slice: {Time}");
        }

        public void LogOpen() {
            var security = Securities[_symbol];
            Debug($"{_symbol} open: {security.LocalTime} ({security.Exchange.TimeZone}) = {Time} ({TimeZone})");
        }

        public override void OnEndOfDay(Symbol symbol) {
            var security = Securities[_symbol];
            Debug($"{_symbol} EOD: {security.LocalTime} ({security.Exchange.TimeZone}) = {Time} ({TimeZone})");
        }
    }
}
