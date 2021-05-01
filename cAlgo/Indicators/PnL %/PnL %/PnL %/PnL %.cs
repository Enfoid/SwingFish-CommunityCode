using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PnlPercent : Indicator
    {
        [Parameter(DefaultValue = 0, Step = 0.01)]
        public double Percent { get; set; }

        private class PriceLevel
        {
            public ChartHorizontalLine LineObject { get; set; }
            public ChartText TextObject { get; set; }
        }

        private Dictionary<TradeType, List<Position>> _positions;
        private Dictionary<TradeType, PriceLevel> _priceLevel;

        protected override void Initialize()
        {
            Percent /= 100;

            var symbolPositions = Positions.Where(x => x.SymbolCode == Symbol.Code).ToList();
            _positions = new Dictionary<TradeType, List<Position>> 
            {
                {
                    TradeType.Buy,
                    new List<Position>(symbolPositions.Where(x => x.TradeType == TradeType.Buy))
                },
                {
                    TradeType.Sell,
                    new List<Position>(symbolPositions.Where(x => x.TradeType == TradeType.Sell))
                }
            };
            _priceLevel = new Dictionary<TradeType, PriceLevel> 
            {
                {
                    TradeType.Buy,
                    null
                },
                {
                    TradeType.Sell,
                    null
                }
            };

            CalculatePnlPrice(TradeType.Buy);
            CalculatePnlPrice(TradeType.Sell);

            Positions.Opened += args =>
            {
                if (args.Position.SymbolCode == Symbol.Code)
                {
                    _positions[args.Position.TradeType].Add(args.Position);
                    CalculatePnlPrice(args.Position.TradeType);
                }
            };

            Positions.Closed += args =>
            {
                if (_positions[args.Position.TradeType].Contains(args.Position))
                {
                    _positions[args.Position.TradeType].Remove(args.Position);
                    CalculatePnlPrice(args.Position.TradeType);
                }
            };

            Positions.Modified += args =>
            {
                if (_positions[TradeType.Buy].Contains(args.Position) || _positions[TradeType.Sell].Contains(args.Position))
                {
                    if (_positions[TradeType.Buy].Contains(args.Position) && args.Position.TradeType == TradeType.Sell)
                    {
                        _positions[TradeType.Buy].Remove(args.Position);
                        _positions[TradeType.Sell].Add(args.Position);
                    }
                    else if (_positions[TradeType.Sell].Contains(args.Position) && args.Position.TradeType == TradeType.Buy)
                    {
                        _positions[TradeType.Sell].Remove(args.Position);
                        _positions[TradeType.Buy].Add(args.Position);
                    }

                    CalculatePnlPrice(TradeType.Buy);
                    CalculatePnlPrice(TradeType.Sell);
                }
            };

            Chart.ScrollChanged += args =>
            {
                foreach (var priceLevel in _priceLevel.Values)
                {
                    if (priceLevel != null)
                    {
                        priceLevel.TextObject.Time = MarketSeries.OpenTime[Chart.LastVisibleBarIndex];
                    }
                }
            };
        }

        private void CalculatePnlPrice(TradeType tradeType)
        {
            if (_positions[tradeType].Count == 0)
            {
                if (_priceLevel[tradeType] != null)
                {
                    Chart.RemoveObject(_priceLevel[tradeType].LineObject.Name);
                    Chart.RemoveObject(_priceLevel[tradeType].TextObject.Name);

                    _priceLevel[tradeType] = null;
                }

                return;
            }

            double volume = 0, entryPrice = 0, commissions = 0, swaps = 0;
            foreach (var position in _positions[tradeType])
            {
                volume += position.VolumeInUnits;
                entryPrice += position.EntryPrice * position.VolumeInUnits;
                commissions += position.Commissions;
                swaps += position.Swap;
            }

            var pips = (Account.Balance * Percent + commissions + swaps) / (volume * Symbol.PipValue);
            var price = entryPrice / volume + pips * Symbol.PipSize * (tradeType == TradeType.Buy ? 1 : -1);
            //           var color = tradeType == TradeType.Buy ? Chart.ColorSettings.BuyColor : Chart.ColorSettings.SellColor;
            var color = Color.SlateGray;
            _priceLevel[tradeType] = new PriceLevel 
            {
                LineObject = Chart.DrawHorizontalLine("line" + tradeType, price, color, 1, LineStyle.Dots),
//                TextObject = Chart.DrawText("text" + tradeType, string.Format("{0}: {1:P} @{2}", tradeType, Percent, Math.Round(price, Symbol.Digits)), Chart.LastVisibleBarIndex, price, color)
//                TextObject = Chart.DrawText("text" + tradeType, string.Format("{0}: {1:P}", tradeType, Percent), Chart.LastVisibleBarIndex, price, color)
                TextObject = Chart.DrawText("text" + tradeType, string.Format("{0}", tradeType), Chart.LastVisibleBarIndex, price, color)
            };

            _priceLevel[tradeType].TextObject.HorizontalAlignment = HorizontalAlignment.Left;
        }

        public override void Calculate(int index)
        {

            // Breakeven Line
            double _avrgB = 0;
            double _avrgS = 0;
            double _lotsB = 0;
            double _lotsS = 0;


            double _avrgA = 0;
            double _lotsA = 0;

            double _tpB = 0;
            double _slB = 0;
            double _tpS = 0;
            double _slS = 0;

            foreach (var position1 in Account.Positions)
            {

                if (Symbol.Code == position1.SymbolCode)
                {

                    if (position1.TradeType == TradeType.Sell)
                    {
                        _avrgB = _avrgB + (position1.Volume * position1.EntryPrice);
                        _lotsB = _lotsB + position1.Volume;
                    }

                    if (position1.TradeType == TradeType.Buy)
                    {
                        _avrgS = _avrgS + (position1.Volume * position1.EntryPrice);
                        _lotsS = _lotsS + position1.Volume;
                    }


                }

            }

            _avrgB = Math.Round(_avrgB / _lotsB, Symbol.Digits);
            _avrgS = Math.Round(_avrgS / _lotsS, Symbol.Digits);

            if (_avrgS > 0)
            {
                ChartObjects.DrawLine("brlineS", index - 2, _avrgS, index + 5, _avrgS, Colors.Lime);
                var brlineSL = Chart.DrawHorizontalLine("Average Buy Price", _avrgS, Color.Lime, 0);
                brlineSL.IsInteractive = true;

            }

            else
            {
                ChartObjects.RemoveObject("brlineS");
                ChartObjects.RemoveObject("Average Buy Price");
            }

            if (_avrgB > 0)
            {
                ChartObjects.DrawLine("brlineL", index - 2, _avrgB, index + 5, _avrgB, Colors.Crimson);
                var brlineLL = Chart.DrawHorizontalLine("Average Sell Price", _avrgB, Color.Crimson, 0);
                brlineLL.IsInteractive = true;
            }
            else
            {
                ChartObjects.RemoveObject("brlineL");
                ChartObjects.RemoveObject("Average Sell Price");
            }
        }
    }
}
