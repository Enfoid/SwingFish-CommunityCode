using cAlgo.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;


/*
ToDo:
- send a actual Notification on Margin cross
- position Notification ?

*/

namespace cAlgo.Robots
{
    public enum Icon
    {
 //       Ctrader = 45063, // cTrader Logo Static
        Ctrader = 45185, // cTrader Logo Animated
        GreenArrowMovingUp = 45186,
        RedArrowMovingDown = 45187,
        Arrows = 20953,
        Warning = 7921,
        Hourglass = 35196,
        Terminal = 315,
        Check = 234,
        Chart = 1989,
        Null = 0,
    }

    public class Frame
    {
        public string text { get; set; }
        public int icon { get; set; }
        public int index { get; set; }

        public Frame(Icon icon, string text)
        {
            this.icon = (int) icon;
            this.text = text;
        }
    }

    public class Frames : List<Frame>
    {
        public string ToJson()
        {
            for (var i = 0; i < this.Count; i++)
            {
                this[i].index = i;
            }

            return new JavaScriptSerializer().Serialize(new { frames = this });
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class LaMetric : Robot
    {
        [Parameter("Local Push URL", Group = "LaMetric", DefaultValue = "https://developer.lametric.com/applications/list")]
        public string Url { get; set; }

        [Parameter("App Access Token", Group = "LaMetric", DefaultValue = "https://developer.lametric.com/applications/list")]
        public string AccessToken { get; set; }

        // will be used later for Push
        [Parameter("Clock API Token", Group = "BETA - DO NOT USE IN Production!", DefaultValue = "https://developer.lametric.com/user/devices")]
        public string APIToken { get; set; }

        [Parameter("Show position details", Group = "BETA - DO NOT USE IN Production!", DefaultValue = true)]
        public bool ShowPositions { get; set; }

        [Parameter("Update Clock (20s)", Group = "LaMetric", DefaultValue = 20)]
        public int TimerDelay { get; set; }

        [Parameter("Day Start Balance", Group = "cTrader", DefaultValue = 0)]
        public double DayStart { get; set; }

        [Parameter("Wife Mode (Auto select Balance/Equity) for Today", Group = "cTrader", DefaultValue = true)]
        public bool WifeMode { get; set; }

        [Parameter("Margin Warning Level", Group = "cTrader", DefaultValue = 3000)]
        public int MarginWarning { get; set; }

        [Parameter("Always Show Margin", Group = "cTrader", DefaultValue = true)]
        public bool ShowMargin { get; set; }

        [Parameter("Show Money", Group = "cTrader", DefaultValue = false)]
        public bool ShowMoney { get; set; }

        public double todayProfit;
        public double todayProfitB;
        public double unrealizedProfit;
 
        protected override void OnStart()
        {
            Positions.Closed += PositionsOnClosed;
            if (DayStart == 0)
            {
                DayStart = AccountBalanceAtTime(Time.Date);
            }

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            UpdateLaMetric();

            Timer.Start(TimeSpan.FromMilliseconds((TimerDelay * 1000)));
        }

        protected override void OnBar()
        {
            UpdateLaMetric();
        }

        protected override void OnTimer() { 
            if (Positions.Count > 0)
                // only use the timer if a position is active
                UpdateLaMetric();
        }

        private void PositionsOnClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            // update when positions close (pypass the timer)
            UpdateLaMetric();
        }

        protected void UpdateLaMetric()
        {
            var frames = new Frames();

            if (ShowMoney) {
                todayProfit = Account.Equity - DayStart;
                todayProfitB = Account.Balance - DayStart;
                unrealizedProfit = Account.Equity - Account.Balance;

                if (WifeMode && (todayProfit < todayProfitB)) {
                    todayProfit = Account.Balance - DayStart;
                }
            }
            else {
                todayProfit = Account.Equity / DayStart * 100 - 100;
                todayProfitB = Account.Balance / DayStart * 100 - 100;
                unrealizedProfit = Account.Equity / Account.Balance * 100 - 100;

                if (WifeMode && (todayProfit < todayProfitB)) {
                    todayProfit = Account.Balance / DayStart * 100 - 100;
                }
            }

            if (Positions.Count > 0)
            {
                if ((ShowMargin) || (Account.MarginLevel.Value < MarginWarning)) { frames.Add(GetMarginFrame()); }
 //               frames.Add(GetTextFrame(Icon.Hourglass, "PnL"));
 //               frames.Add(GetValueFrame(unrealizedProfit, true));
 //               frames.Add(GetTextFrame(Icon.Hourglass, "PnL"));
                if (ShowMoney) 
                {
                    frames.Add(GetValueFrame(unrealizedProfit, false));
                }
                else {
                    frames.Add(GetValueFrame(unrealizedProfit, true));
                }
                // if (ShowPositions) 
                // show buy/sell what
 //               frames.Add(GetTextFrame(Icon.Null, "Session"));
 //               frames.Add(GetValueFrame(todayProfit, true));
            }
            else
            {
//                frames.Add(GetValueFrame(todayProfit, true));
                if (todayProfit ==0) {
                        frames.Add(GetTextFrame(Icon.Ctrader, "Ctrader"));
                }
                else {
                    if (ShowMoney) {
                        if (todayProfit >0) {
                            frames.Add(GetTextFrame(Icon.Ctrader, "$" + Math.Round(todayProfit, Math.Abs(todayProfit) > 1000 ? 0 : 3) + " Session Profit"));
                        } else {
                            frames.Add(GetTextFrame(Icon.Ctrader, "$" + Math.Round(todayProfit, Math.Abs(todayProfit) > 1000 ? 0 : 3) + " Session Loss"));
                        }
                    }
                    else {
                        if (todayProfit >0) {
                            frames.Add(GetTextFrame(Icon.Ctrader, "+" + Math.Round(todayProfit,3) + "% Session Profit"));
                        } else {
                            frames.Add(GetTextFrame(Icon.Ctrader, Math.Round(todayProfit,3) + "% Session Loss"));
                        }
                    }
                }
            }

            SendFramesAsync(frames);
        }

        private Frame GetTextFrame(Icon icon, string text) {
            return new Frame(icon, text);
        }

        private Frame GetMarginFrame()
        {
            var text = "-/-";
            var icon = Icon.Terminal;

            if (Account.MarginLevel.HasValue == false) return new Frame(icon, text);
            if (Account.MarginLevel > 10000)
            {
                text = " > 10k";
            }
            else
            {
                text = Math.Round(Account.MarginLevel.Value, 0) + "%";

                if (Account.MarginLevel < MarginWarning)
                {
                    icon = Icon.Warning;
                }
            }

            return new Frame(icon, text);
        }

        protected static Frame GetValueFrame(double value, bool isPercentage = false)
        {
            var text = "";
            var icon = value >= 0 ? Icon.GreenArrowMovingUp : Icon.RedArrowMovingDown;
            if (isPercentage) {
                text = (isPercentage ? "" : "$") + Math.Round(value, Math.Abs(value) > 9 ? 2 : 3) + (isPercentage ? "%" : "");
            } else {
                var pnl ="";
                if ( value > 1000) {
                    text = "$"+Math.Abs(Math.Round(value, 0));
                }
                else {
                    text = "$" + Math.Round(value, 2).ToString("0.00") ;
                }
            }
            return new Frame(icon, text);
        }

        protected override void OnStop()
        {
            // send logo to the clock
            SendFramesAsync(new Frames { new Frame(Icon.Ctrader, "cTrader") });
        }

        private double AccountBalanceAtTime(DateTime dt)
        {
            var historicalTrade = History.LastOrDefault(x => x.ClosingTime < dt);
            return historicalTrade != null ? historicalTrade.Balance : Account.Balance;
        }

        private async Task<HttpResponseMessage> SendFramesAsync(Frames frames)
        {
            var json = frames.ToJson();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(Url),
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Headers =
                {
                    { "Accept", "application/json" },
                    { "X-Access-Token", AccessToken },
                    { "Cache-Control", "no-cache" }
                }
            };

            var httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        
            using (var client = new HttpClient(httpClientHandler))
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                return await client.SendAsync(request);
            }
        }
    }
}