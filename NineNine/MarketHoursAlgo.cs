using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Securities;
using System;
using System.Text;

namespace NineNine {

    /// <summary>
    // Print market hours for symbol
    /// </summary>
    public class MarketHoursAlgo : QCAlgorithm {

        /// <summary>
        /// Initialize algorithm.
        /// </summary>
        public override void Initialize() {
            Debug($"Algorithm Mode: {AlgorithmMode}. Deployment Target: {DeploymentTarget}. TimeZone: {TimeZone.Id}");
            
            SetCash(100000);
            SetStartDate(DateTime.Now.AddDays(-1).Date);
            SetEndDate(DateTime.Now.Date);

            var security = AddCfd("SPX500USD");
            var symbol = security.Symbol;

            // get market hours
            var hours = security.Exchange.Hours;
            //hours = MarketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);

            var sb = new StringBuilder();
            sb.AppendLine($"Local market hours for {symbol.Value} ({hours.TimeZone}):");
            foreach (var day in hours.MarketHours.Values) {
                if (day.IsOpenAllDay || day.IsClosedAllDay) {
                    sb.AppendLine($"  {day.DayOfWeek}: {day}");
                } else {
                    sb.AppendLine($"  {day}");
                }
            }
            Debug(sb.ToString());
        }

    }
}
