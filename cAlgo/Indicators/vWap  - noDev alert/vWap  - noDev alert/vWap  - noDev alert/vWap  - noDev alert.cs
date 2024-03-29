﻿using System;
using System.Speech.Synthesis;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Collections.Generic;


namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    //TimeZone = TimeZones.UTC
    public class IntraDayStandardDeviation : Indicator
    {
        [Parameter("MA Type")]
        public MovingAverageType matype { get; set; }

        [Parameter("Period", DefaultValue = 200)]
        public int atrper { get; set; }

        [Output("atrpips")]
        public IndicatorDataSeries Result { get; set; }


        AverageTrueRange atr;
        ExponentialMovingAverage emaatr;

//        private MovingAverage movingAverage;
//        private StandardDeviation standardDeviation;

        [Parameter("Offset Reset (candles)", DefaultValue = 0)]
        public int TimeOffset { get; set; }

        [Parameter("Show vWap History", DefaultValue = false)]
        public bool ShowHistoricalvWap { get; set; }

        [Parameter("Alert Active", DefaultValue = true)]
        public bool VwapAlertActive { get; set; }

        [Parameter("change BG", DefaultValue = false)]
        public bool PaintChart { get; set; }

        [Parameter("BG Opacity", DefaultValue = 255, MinValue = 0, MaxValue = 255)]
        public int Opc { get; set; }

        [Parameter("Alert distance", DefaultValue = 1)]
        public double VwapAlertDistance { get; set; }

        [Parameter("Alert Volume", DefaultValue = 100, MinValue = 0, MaxValue = 100)]
        public int AlertVolume { get; set; }

        [Parameter("Corner for Infos", DefaultValue = 0, MinValue = 0, MaxValue = 4)]
        public int corner { get; set; }

        [Output("VWAP", PlotType = PlotType.Points, Thickness = 2, Color = Colors.Yellow)]
        public IndicatorDataSeries VWAP { get; set; }

//      crappy cTrader 3.7 fix ?
        private MarketSeries _ms;
        public new MarketSeries MarketSeries
        {
            get
            {
                if (_ms == null)
                    _ms = base.MarketSeries;
                return _ms;
            }
        }

        private SpeechSynthesizer _synthesizer;

//       private int end_bar = 0;
        private int start_bar = 0;
        private int oldCurrentDay = 0;

        public StaticPosition corner_position;
        public int CurrentDay = 0;

        private bool Say(string Sim)
        {
            _synthesizer.Speak(Sim);
            return true;
        }

        private string CmdArgs;
        private bool Say2(string Sim)
        {
            CmdArgs = "-Command \"Add-Type –AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('" + Sim + "');\"";
            {
                Process myProcess = new Process();
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.Arguments = CmdArgs;
                myProcess.StartInfo.FileName = "PowerShell";
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.Start();
                myProcess.WaitForExit();
                return true;
            }
        }

        bool alertDone;
        // Function to amend the sentence 
        private string amendOut;
        public string amendSentence(string sstr)
        {
//          general replacements
            sstr = sstr.Replace("XAUUSD", "gold");
            sstr = sstr.Replace("XAGUSD", "silver");
            sstr = sstr.Replace("US30", "dow jones");
            sstr = sstr.Replace("US500", "S P X");
            sstr = sstr.Replace("US2000", "Russel 2k");
            sstr = sstr.Replace("JPN225", "nikkei");
            sstr = sstr.Replace("JP225", "nikkei");
            sstr = sstr.Replace("GER30", "dax");
            sstr = sstr.Replace("de30", "dax");
            sstr = sstr.Replace("USDX", "dollar index");
            sstr = sstr.Replace("DXY", "dollar index");
            sstr = sstr.Replace("GBPUSD", "cable");
            sstr = sstr.Replace("NAS100", "Nasdaq");
            sstr = sstr.Replace("HK50", " HengSeng ");
            sstr = sstr.Replace("XBRUSD", "Crude Oil");

//          Currency parts
            sstr = sstr.Replace("CAD", " cad ");
            sstr = sstr.Replace("GBP", " pound ");
            sstr = sstr.Replace("NZD", " kiwi ");
            sstr = sstr.Replace("JPY", " yen ");
            sstr = sstr.Replace("AUD", " ozzy ");
            sstr = sstr.Replace("EUR", " euro ");
            sstr = sstr.Replace("SGD", " sing ");
            sstr = sstr.Replace("USD", " dollar ");

            amendOut = "";
            char[] str = sstr.ToCharArray();

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] >= 'A' && str[i] <= 'Z')
                {
                    str[i] = (char)(str[i] + 32);
                    if (i != 0)
                    {
                        amendOut = amendOut + " ";
                    }

                    amendOut = amendOut + str[i];
                }
                else
                    amendOut = amendOut + str[i];
            }
            return amendOut + " ";
        }

        private int _lastBarIndex;
        private void OnBarClosed(int index)
        {
            alertDone = false;
        }
        protected override void Initialize()
        {
//            movingAverage = Indicators.MovingAverage(VWAP, 14, MovingAverageType.Simple);
//            standardDeviation = Indicators.StandardDeviation(VWAP, 14, MovingAverageType.Simple);
            atr = Indicators.AverageTrueRange(atrper, MovingAverageType.Simple);

            _synthesizer = new SpeechSynthesizer 
            {
                Volume = AlertVolume,
                Rate = -1

            };
        }
        public override void Calculate(int index)
        {
            switch (corner)
            {
                case 1:
                    corner_position = StaticPosition.TopLeft;
                    break;
                case 2:
                    corner_position = StaticPosition.TopRight;
                    break;
                case 3:
                    corner_position = StaticPosition.BottomLeft;
                    break;
                case 4:
                    corner_position = StaticPosition.BottomRight;
                    break;
            }
            int end_bar = index;
            int CurrentDay = MarketSeries.OpenTime[end_bar].DayOfYear;
            double TotalPV = 0;
            double TotalVolume = 0;
            double highest = 0;
            double lowest = 999999;
            double close = MarketSeries.Close[index];

            if (index > _lastBarIndex)
            {
                OnBarClosed(index - 1);
                _lastBarIndex = index;
            }



            if (VwapAlertActive == true)
            {
                if ((!alertDone) && ((Math.Round(VWAP[index] - 1, Symbol.Digits) >= Symbol.Bid) && (Math.Round(VWAP[index] + 1, Symbol.Digits) <= Symbol.Bid)))
                {
                    Say(amendSentence(Symbol.Code) + " ");
                    alertDone = true;
                }
                if ((!alertDone) && (Math.Abs(VWAP[index] - Symbol.Bid) < (Symbol.TickSize * VwapAlertDistance)))
                {
                    Say(amendSentence(Symbol.Code) + " ");
                    alertDone = true;
                }
            }
            if (CurrentDay == oldCurrentDay)
            {
                for (int i = start_bar; i <= end_bar; i++)
                {
                    TotalPV += MarketSeries.TickVolume[i] * ((MarketSeries.Low[i] + MarketSeries.High[i] + MarketSeries.Close[i]) / 3);
                    TotalVolume += MarketSeries.TickVolume[i];
                    VWAP[i] = TotalPV / TotalVolume;

                    if (MarketSeries.High[i] > highest)
                    {
                        highest = MarketSeries.High[i];
                    }
                    if (MarketSeries.Low[i] < lowest)
                    {
                        lowest = MarketSeries.Low[i];
                    }


                    if (corner != 0)
                        ChartObjects.DrawText("show", "vWap " + Math.Round(VWAP[index], Symbol.Digits) + "P " + Symbol.Bid, corner_position);

                    if (!ShowHistoricalvWap)
                    {
                        //  VWAP[index] = sum / start_bar - i;
                        if (i < index - 15)
                        {
                            VWAP[i] = double.NaN;
                        }
                    }
                }
            }
            else
            {
                if (!ShowHistoricalvWap)
                {
                    for (int i = index - 16; i <= index; i++)
                    {
                        VWAP[i] = double.NaN;
                    }
                }
                oldCurrentDay = MarketSeries.OpenTime[end_bar].DayOfYear;
                start_bar = end_bar - TimeOffset;
            }
            if (IsLastBar && PaintChart)
            {

                double cl1 = (((atr.Result[index] / Symbol.PipSize)) * 2);

                double vWapDistanceFactor = 255 * (cl1 / Math.Round((Math.Abs(VWAP[index] - Symbol.Bid) / Symbol.PipSize)));

                int colorfactor = 0;

                if (vWapDistanceFactor > 255)
                {
                    colorfactor = 255;
                }
                else
                {
                    colorfactor = Convert.ToInt32(vWapDistanceFactor);
                }

                Print("atr" + cl1 + " df " + vWapDistanceFactor + " cf " + colorfactor);

//                double cl1i = (((VWAP[index] - movingAverage.Result[index]) / standardDeviation.Result[index]) * 50);
//                int cl1 = Math.Abs((int)cl1i);
//                Print(((VWAP[index] - movingAverage.Result[index]) / standardDeviation.Result[index]));

//                int red = (int)Math.Max(VWAP[index] / VWAP[1], 0);
//                int green = (int)Math.Max(VWAP[index], 0);
//                Chart.ColorSettings.BackgroundColor = Color.FromArgb(Opc, red, green, 0);
                if (VWAP[index] < Symbol.Bid)
                {
//                    Chart.ColorSettings.BackgroundColor = Color.DarkGreen;
//                    Chart.ColorSettings.BackgroundColor = Color.FromArgb(Opc, 0, 50, 0);
                    Chart.ColorSettings.BackgroundColor = Color.FromArgb(Opc, 0, colorfactor, 0);
                }
                else
                {
//                    Chart.ColorSettings.BackgroundColor = Color.SaddleBrown;
//                    Chart.ColorSettings.BackgroundColor = Color.FromArgb(Opc, 70, 0, 0);
                    Chart.ColorSettings.BackgroundColor = Color.FromArgb(Opc, colorfactor, 0, 0);
                }
//                Print(cl1);
            }
            return;
        }
    }
}
