using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class StopSyncer : Robot
    {

        [Parameter("Action Price", DefaultValue = 0)]
        public double requestedPrice { get; set; }


        [Parameter("TP", DefaultValue = 0)]
        public double fTP { get; set; }

        [Parameter("use Average", DefaultValue = false)]
        public bool useAverage { get; set; }

        [Parameter("SL", DefaultValue = 0)]
        public double fSL { get; set; }

        [Parameter("Trail from SL", DefaultValue = false)]
        public bool TPT { get; set; }

        public double orderSL = 0;
        public double orderTP = 0;
        public double avBuy = 0;
        public double avSell = 0;


        protected override void OnStart()
        {
            ModifyOrders();
        }

        protected override void OnTick()
        {
            // Put your core logic here
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
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

        private void ModifyOrders()
        {

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
            Stop();
        }

    }


}


