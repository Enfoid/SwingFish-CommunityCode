using System;
using cAlgo.API;
using cAlgo.Client;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class StopSyncer2 : Robot
    {
        [Parameter("Vertical Position", Group = "Panel alignment", DefaultValue = VerticalAlignment.Top)]
        public VerticalAlignment PanelVerticalAlignment { get; set; }

        [Parameter("Horizontal Position", Group = "Panel alignment", DefaultValue = HorizontalAlignment.Left)]
        public HorizontalAlignment PanelHorizontalAlignment { get; set; }


//        public double requestedPrice = 0;
        public double? fTP = 0;

        public bool useAverage = false;
        public double? fSL = 0;

        public bool TPT = false;

        public double orderSL = 0;
        public double orderTP = 0;
        public double avBuy = 0;
        public double avSell = 0;

        private Client.Control _control;

        private const string Version = "v2.04";

        protected override void OnStart()
        {
            _control = new Client.Control(Version);
            _control.ActionPriceSubmit += OnActionPriceSubmit;
            _control.ProfitStopSubmit += OnProfitStopSubmit;
            _control.ClearAllButton.Click += OnClearAll;

            var isBottomLeft = PanelVerticalAlignment == VerticalAlignment.Bottom && PanelHorizontalAlignment == HorizontalAlignment.Left;

            Chart.AddControl(new Border 
            {
                VerticalAlignment = PanelVerticalAlignment,
                HorizontalAlignment = PanelHorizontalAlignment,
                Margin = isBottomLeft ? "6 6 6 36" : "6",
                MinWidth = 180,
                Child = _control
            });
        }

        private void OnClearAll(ButtonClickEventArgs obj)
        {
            Print("Clear All clicked");
            fTP = 0;
            fSL = 0;
            TPT = false;

            //           requestedPrice = actionPrice;
            useAverage = false;

            ModifyOrders(null);
        }

//        private void OnActionPriceSubmit(double actionPrice, bool useAvg, bool trailStop)
        private void OnActionPriceSubmit(double? actionPrice, bool useAvg, bool trailStop)
        {
            Print("Action Price = {0}; Use AVG = {1}; TSL = {2}", actionPrice, useAvg, trailStop);
            fTP = 0;
            fSL = 0;
            TPT = trailStop;

            //           requestedPrice = actionPrice;
            useAverage = useAvg;

            ModifyOrders(actionPrice);
//            ModifyOrders(actionPrice, useAvg, trailStop, false, false);

        }


        private void OnProfitStopSubmit(double? takeProfit, double? stopLoss, bool useAvg, bool trailStop)
        {
            Print("TP = {0}; SL = {1}; Use AVG = {2}; TSL = {3}", takeProfit, stopLoss, useAvg, trailStop);
            fTP = takeProfit;
            fSL = stopLoss;
            TPT = trailStop;

            //requestedPrice = 0;
            useAverage = false;

            ModifyOrders(null);

        }

        private void ModifyOrders(double? r2)
        {

            double requestedPrice = 0;

            if (r2.HasValue)
            {
                requestedPrice = (double)r2;
            }
            else
            {
                requestedPrice = 0;
            }
            foreach (var pos in Positions)
            {
                if (pos.SymbolCode == Symbol.Code)
                {
                    // if buy and sl lower = sl = tp
                    // if buy and sl higher = sl = sl
                    // .. and so on

                    // long position, prices need to be higher 
                    if (((fTP != 0) || (fSL != 0)) && (requestedPrice == 0))
                    {
                        // we have ghard TP/SL .. just set it and let it be ognore action price
                        if (TPT)
                        {
                            ModifyPositionAsync(pos, fSL, fTP, true);
                        }
                        else
                        {
                            ModifyPositionAsync(pos, fSL, fTP, false);
                        }
                    }
                    else
                    {

                        if (pos.TradeType == TradeType.Buy)
                        {
                            if (useAverage)
                            {
                                // overrides the price field
                                updateAverages();
                                requestedPrice = avBuy;
                            }
                            if (Symbol.Bid < requestedPrice)
                            {
                                // price is below SL use it as TP
                                orderSL = 0;
                                orderTP = requestedPrice;
                            }
                            else
                            {
                                // prince higher than SL use SL as SL 
                                orderSL = requestedPrice;
                                orderTP = 0;
                            }
                        }
                        else
                        {
                            // short position 
                            if (useAverage)
                            {
                                // overrides the price field
                                updateAverages();
                                requestedPrice = avSell;
                            }
                            if (Symbol.Bid < requestedPrice)
                            {
                                // price is below SL use it as TP
                                orderSL = requestedPrice;
                                orderTP = 0;
                            }
                            else
                            {
                                // prince higher than SL use SL as SL 
                                orderSL = 0;
                                orderTP = requestedPrice;
                            }
                        }
                        if (TPT && orderSL != 0)
                        {
                            ModifyPositionAsync(pos, orderSL, orderTP, true);
                        }
                        else
                        {
                            ModifyPositionAsync(pos, orderSL, orderTP, false);
                        }
                    }


                }
            }
        }

        public void updateAverages()
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

                    if (position1.TradeType == TradeType.Buy)
                    {
                        _avrgB = _avrgB + (position1.Volume * position1.EntryPrice);
                        _lotsB = _lotsB + position1.Volume;
                    }

                    if (position1.TradeType == TradeType.Sell)
                    {
                        _avrgS = _avrgS + (position1.Volume * position1.EntryPrice);
                        _lotsS = _lotsS + position1.Volume;
                    }


                }

            }

            avBuy = Math.Round(_avrgB / _lotsB, Symbol.Digits);
            avSell = Math.Round(_avrgS / _lotsS, Symbol.Digits);
        }

    }
}
