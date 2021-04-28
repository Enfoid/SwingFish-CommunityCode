﻿using cAlgo.API;
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

/* Changelog
- Better communication
- fixed icon issue
- added margin warning level (shows a different icon when level is crossed)
- only shows "today" when no position is open


ToDo:
- send a actual Notification on Margin cross
- position Notification ?
- OnStop needs to wait a bit to complete the last transmission .. bot quits too fast last update of the clock does not work

*/

namespace cAlgo.Robots
{
    public enum Icon
    {
        Ctrader = 20953,
        GreenArrowMovingUp = 7465,
        RedArrowMovingDown = 7463,
        Warning = 7921,
        Hourglass = 35196,
        Terminal = 315,
        Check = 234,
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
        [Parameter("Clock API Token", Group = "BETA - Not used!", DefaultValue = "https://developer.lametric.com/user/devices")]
        public string APIToken { get; set; }

        [Parameter("Update Clock (20s)", Group = "LaMetric", DefaultValue = 20)]
        public int TimerDelay { get; set; }

        [Parameter("Show Margin", Group = "BETA - Not used!", DefaultValue = true)]
        public bool ShowMargin { get; set; }

        [Parameter("Day Start Balance", Group = "cTrader", DefaultValue = 0)]
        public double DayStart { get; set; }

        [Parameter("Auto select (Balance/Equity) for Today", Group = "cTrader", DefaultValue = true)]
        public bool RunningEquityProfit { get; set; }

        [Parameter("Margin Warning Level", Group = "cTrader", DefaultValue = 3000)]
        public int MarginWarning { get; set; }

        protected override void OnStart()
        {
            if (DayStart == 0)
            {
                DayStart = AccountBalanceAtTime(Time.Date);
            }

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            UpdateLaMetric();

            Timer.Start(TimeSpan.FromMilliseconds((TimerDelay * 1000)));
        }

        protected override void OnTick()
        {
            // UpdateLaMetric();
        }

        protected override void OnTimer() { UpdateLaMetric(); }

        protected void UpdateLaMetric()
        {
            var frames = new Frames();

            var todayProfit = Account.Equity / DayStart * 100 - 100;
            var todayProfitB = Account.Balance / DayStart * 100 - 100;
            var unrealizedProfit = Account.Equity / Account.Balance * 100 - 100;

            if (RunningEquityProfit && (todayProfit < todayProfitB)) {
                todayProfit = Account.Balance / DayStart * 100 - 100;
            }

            if (Positions.Count > 0)
            {
                frames.AddRange(new[]
                {
                    GetMarginFrame(),
                    new Frame(Icon.Hourglass, "PnL"),
                    GetValueFrame(unrealizedProfit, true),
                    new Frame(Icon.Hourglass, "Profit Today"),
                    GetValueFrame(todayProfit, true),
                });
            }
            else
            {
                frames.AddRange(new[]
                {
                    new Frame(Icon.Check, "Profits Today"),
                    GetValueFrame(todayProfit, true),
                });
            }

            SendFramesAsync(frames);
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

        private static Frame GetValueFrame(double value, bool isPercentage = false)
        {
            var icon = value > 0 ? Icon.GreenArrowMovingUp : Icon.RedArrowMovingDown;
            var text = Math.Round(value, value > 10 ? 2 : 3) + (isPercentage ? "%" : "");

            return new Frame(icon, text);
        }

        protected override void OnStop() { SendFramesAsync(new Frames { new Frame(Icon.Ctrader, "cTrader") }); }

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

            using (var client = new HttpClient())
            {
                return await client.SendAsync(request);
            }
        }
    }
}