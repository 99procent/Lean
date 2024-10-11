using MathNet.Numerics.Statistics;
using QLNet;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Scheduling;
using QuantConnect.Securities;
using System;
using System.Linq;
using System.Text;

namespace NineNine {

    /// <summary>
    /// Algo that randomly selects a new symbol every day.
    /// Uses ETFs in backtesting and their CFD equivalents in live mode (see https://www.quantconnect.com/docs/v2/writing-algorithms/securities/asset-classes/cfd/requesting-data),.
    /// </summary>
    public class RandomDailyAlgo : QCAlgorithm {
        // Top 25 ETFs according to https://www.marketwatch.com/tools/top-25-etfs
        private string[] _tickers = new[] { "SPY", "IVV", "VOO", "VTI", "QQQ", "VEA", "VUG", "VTV", "IEFA", "AGG", "BND", "IWF", "IJH", "VIG", "IJR", "IEMG", "VWO", "VXUS", "VGT", "GLD", "XLK", "VO", "IWM", "RSP", "ITOT" };
        private string _market = Market.USA;
        private SecurityType _securityType = SecurityType.Equity;

        // keys for custom data
        private const string ENTRY_PRICE = nameof(ENTRY_PRICE);
        private const string STANDARD_DEVIATION = nameof(STANDARD_DEVIATION);
        private const string SKEWNESS = nameof(SKEWNESS);
        private const string KURTOSIS = nameof(KURTOSIS);
        private const string TRAILING_STOP = nameof(TRAILING_STOP);
        private const string QUANTIITY = nameof(QUANTIITY);

        private const Resolution RESOLUTION = Resolution.Minute;

        // selected symbol
        private Symbol _symbol;

        // scheduling event to open position after market open
        private ScheduledEvent openEvent;

        /// <summary>
        /// Initialize algorithm.
        /// </summary>
        public override void Initialize() {
            SetTimeZone("Europe/Stockholm");
            SetStartDate(2024, 01, 01);
            SetEndDate(2024, 10, 01);
            SetCash(100000);

            Debug($"Algorithm Mode: {AlgorithmMode}. Deployment Target: {DeploymentTarget}. TimeZone: {TimeZone.Id}");

            // set properties depending on algorith mode and platform
            if (AlgorithmMode == AlgorithmMode.Backtesting && DeploymentTarget == DeploymentTarget.LocalPlatform) {
                // for local testing we only have (free) data for the following symbols and dates
                _tickers = new[] { "AIG", "BAC", "IBM", "SPY" };
                SetStartDate(2013, 10, 04);
                SetEndDate(2013, 10, 11);
            } else if (LiveMode) {
                // use  InteractiveBrokers when algo is running in live mode (see https://www.interactivebrokers.ie/en/trading/products-exchanges.php for list of supported products)
                _market = Market.InteractiveBrokers;
                // use CFD equivalent of ETF
                _securityType = SecurityType.Cfd;
            } else {
                // use ETFs when backtesting
            }

            // select random symbol to start the algo
            SelectSymbol();
        }

        /// <summary>
        /// Manage position
        /// </summary>
        /// <param name="slice"></param>
        public override void OnData(Slice slice) {
            foreach (var kv in slice.QuoteBars) {
                var symbol = kv.Key;
                if (Portfolio[symbol].Invested) {
                    var data = kv.Value;
                    var security = Securities[symbol];

                    Debug($"{Time}: Managing position for {symbol.Value} {data.Price}");

                    // close position when moving in the wrong direction
                    var trailingStop = security.Get<decimal>(TRAILING_STOP);
                    if (data.Price <= trailingStop) {
                        RemoveSecurity(symbol);
                        return;
                    }

                    // increase position when moving in the right direction (up)
                    var entryPrice = security.Get<decimal>(ENTRY_PRICE);
                    var skewness = security.Get<decimal>(SKEWNESS);
                    var kurtosis = security.Get<decimal>(KURTOSIS);                    
                    if (entryPrice <= trailingStop && skewness < 0.5m && kurtosis > 3m) {
                        // scale up by 20%
                        var initialQuantity = security.Get<decimal>(QUANTIITY);
                        var lots = initialQuantity / security.SymbolProperties.LotSize;
                        var quantity = Math.Round(lots * 0.2m);
                        var ticket = MarketOrder(_symbol, quantity);
                        Debug($"{Time}: {ticket}");
                    }

                    // adjust trailing stop
                    var standardDeviation = security.Get<decimal>(STANDARD_DEVIATION);
                    var newStop = data.High - (standardDeviation / 2);
                    if (newStop > trailingStop) {
                        security[TRAILING_STOP] = newStop;
                    }
                    
                } else {
                    //Debug($"{Time}: Skipping {symbol.Value}. Not invested.");
                }
            }
        }

        /// <summary>
        /// Close position on end of day.
        /// </summary>
        /// <param name="symbol"></param>
        public override void OnEndOfDay(Symbol symbol) {
            var security = Securities[_symbol];
            security.TryGet<decimal>(ENTRY_PRICE, out var entryPrice);
            Debug($"{Time}: Closing position for {_symbol} at {security.LocalTime} ({security.Exchange.TimeZone}). Entry: {entryPrice}. Current: {security.Price}");

            RemoveSecurity(symbol);

            // select a new symbol
            SelectSymbol();
        }

        /// <summary>
        /// Log order events.
        /// </summary>
        /// <param name="orderEvent"></param>
        public override void OnOrderEvent(OrderEvent orderEvent) {
            Debug($"{Time}: {orderEvent}");
        }

        /// <summary>
        /// Open position in selected symbol.
        /// </summary>
        private void OpenPosition() {
            var security = Securities[_symbol];

            Debug($"{Time}: Opening position for {_symbol} at {security.LocalTime} ({security.Exchange.TimeZone}). Entry: {security.Price}");

            // Calculate position size based on historic returns
            // REVIEW: use 2% of portfolio cash if position is very small...
            var optimalSize = CalculatePositionSize(security);

            // quantity must be multiple of lot size, and since we want to scale up later without risking to much we multiply it with a scale factor
            var minQuantity = 5 * security.SymbolProperties.LotSize;
            var optimalQuantity = optimalSize == null ? 0 : Math.Round(optimalSize.Value / security.Price);
            var quantity = optimalQuantity < minQuantity ? minQuantity : optimalQuantity;
            
            // TODO: save number of lots

            // Calculate some stats that we need later when managing the position

            // TODO: get closing prices for last 20 days
            var history = Array.Empty<double>();

            // calculate price change in percentage
            var percentage = CalculatePercentageChange(history);

            // calculate skewness and kurtosis
            var (skewness, kurtosis) = percentage.SkewnessKurtosis();

            // calculate standard deviation of closing prices
            var std = history.StandardDeviation();

            // calculate trailing stop 
            var trailingStop = security.Price - ((decimal)std / 2m);

            security[ENTRY_PRICE] = security.Price;
            security[QUANTIITY] = quantity;
            security[STANDARD_DEVIATION] = std;
            security[SKEWNESS] = skewness;
            security[KURTOSIS] = kurtosis;
            security[TRAILING_STOP] = trailingStop;


            // Place market order
            // REVIEW: use trailing stop loss instead of market order, requires converting trailingStop to percentage or amount instead
            var ticket = MarketOrder(_symbol, quantity);
            Debug($"{Time}: {ticket}");
        }

        /// <summary>
        /// Calculate optimal position size (amount) based on historic returns of algo.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private decimal? CalculatePositionSize(Security security) {

            // TODO: If algo hasn't been running for 100 days, we cannot calculate position size
            //if (less than 100 days) {
            //    return null;
            //}

            // TODO: calculate daily returns (in percentage) = daily PnL/Trading capital for the last 100 days, ex day1: PnL = $100, TC = $100000, day2: PnL = -50, TC = $100100, day 3: PnL = 200, TC $100050...
            var returns = Array.Empty<double>();

            // calculate Sharpe Ratio for returns: S(x) = (Rx - Rf) / stdDev(Rx), see https://www.investopedia.com/articles/07/sharpe_ratio.asp
            var (mean, std) = returns.MeanStandardDeviation();
            var rf = 0; // risk free rate

            // TODO: Use TradingDaysPerYear from LEAN config

            var tradingDays = 252;

            var sharpeRatio = (mean - rf) / std;
            var annualizedSharpeRatio = sharpeRatio * Math.Sqrt(tradingDays);
            var kellyFactor = 1;

            // calculate optimal risk
            var riskFactor = (annualizedSharpeRatio / kellyFactor) * annualizedSharpeRatio;

            // annual volatility
            var annualVolatility = Portfolio.Cash * (decimal)riskFactor;

            // daily volatility
            var dailyVolatility = annualVolatility / tradingDays;

            return dailyVolatility;
        }

        /// <summary>
        /// Select random symbol.
        /// </summary>
        private void SelectSymbol() {
            // Select random symbol
            
            // NOTE: should check for stationarity using ADF & KPSS, but for now we assume all symbols are non stationary


            var i = Random.Shared.Next(_tickers.Length);
            _symbol = QuantConnect.Symbol.Create(_tickers[i], _securityType, _market);
            Debug($"{Time}: Selected {_symbol.Value}");

            // Add security to the algorithm
            var security = AddSecurity(_symbol, RESOLUTION);

            // Log market hours
            var hours = security.Exchange.Hours;
            var sb = new StringBuilder();
            sb.AppendLine($"Market hours for {_symbol.Value} ({hours.TimeZone}):");
            foreach (var day in hours.MarketHours.Values) {
                if (day.IsOpenAllDay || day.IsClosedAllDay) {
                    sb.AppendLine($"  {day.DayOfWeek}: {day}");
                } else {
                    sb.AppendLine($"  {day}");
                }
            }
            Debug(sb.ToString());

            // Remove existing event (if any)
            if (openEvent != null) {
                Schedule.Remove(openEvent);
            }

            // Schedule (new) event to open position after market opens
            openEvent = Schedule.On(DateRules.EveryDay(), TimeRules.AfterMarketOpen(_symbol, 10), OpenPosition);
        }

        /// <summary>
        /// Calculates the percentage change between items in an array:
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public double[] CalculatePercentageChange(double[] items) {
            // Array to store the percentage changes
            var percentage = new double[items.Length - 1];

            // Loop through the prices array and calculate the percentage change
            for (int i = 1; i < items.Length; i++) {
                // ((newPrice - oldPrice) / oldPrice) * 100
                percentage[i - 1] = ((items[i] - items[i - 1]) / items[i - 1]) * 100;
            }

            return percentage;
        }
    }
}
