using System;
using System.Diagnostics;
using cAlgo.API;

namespace cAlgo.Client
{
    public class Control : CustomControl
    {
        public delegate void ActionPriceEventHandler(double? actionPrice, bool useAvg, bool trailStop);

        public delegate void ProfitStopEventHandler(double? takeProfit, double? stopLoss, bool useAvg, bool trailStop);

        public event ActionPriceEventHandler ActionPriceSubmit = delegate { };

        public event ProfitStopEventHandler ProfitStopSubmit = delegate { };

        public Button ClearAllButton;

        public Control(string version)
        {
            var panel = new StackPanel();
            var content = new Border { Style = Styles.PanelBackground(), Child = CreateContent() };
            var footer = CreateCopyright(version);

            panel.AddChild(content);
            panel.AddChild(footer);

            AddChild(panel);
        }

        public ControlBase CreateCopyright(string version)
        {
            var button = new Button
            {
                Text = string.Format("{0} - www.swingfish.trade", version),
                ForegroundColor = Color.DodgerBlue,
                BackgroundColor = Color.Transparent,
                Padding = 0,
                Dock = Dock.Bottom,
            };
            button.Click += args => Process.Start("https://swingfish.trade");

            var grid = new Grid(1, 3) { Margin = 5 };
            grid.Columns[0].SetWidthInStars(1);
            grid.Columns[1].SetWidthToAuto();
            grid.Columns[2].SetWidthInStars(1);
            grid.AddChild(button, 0, 1);

            return grid;
        }

        public ControlBase CreateContent()
        {
            var actionPrice = new TextBox();
            var submitActionPrice = new Button { Text = "Apply", Style = Styles.ButtonGreen() };
            var useAvg = new CheckBox { Text = "Use AVG" };
            var trailStop = new CheckBox { Text = "TSL" };
            var takeProfit = new TextBox();
            var stopLoss = new TextBox();
            var submitTpSl = new Button { Text = "Apply TP/SL", Style = Styles.ButtonGreen() };

            ClearAllButton = new Button { Text = "Clear All", Style = Styles.ButtonBlue() };

            submitActionPrice.Click += args =>
            {
                try
                {
                    var price = !string.IsNullOrEmpty(actionPrice.Text) ? double.Parse(actionPrice.Text) : (double?)null;
                    ActionPriceSubmit.Invoke(price, useAvg.IsChecked.Value, trailStop.IsChecked.Value);
                }
                catch (Exception) { }
            };

            submitTpSl.Click += args =>
            {
                try
                {
                    var tp = !string.IsNullOrEmpty(takeProfit.Text) ? double.Parse(takeProfit.Text) : (double?)null;
                    var sl = !string.IsNullOrEmpty(stopLoss.Text) ? double.Parse(stopLoss.Text) : (double?)null;

                    ProfitStopSubmit.Invoke(tp, sl, useAvg.IsChecked.Value, trailStop.IsChecked.Value);
                }
                catch (Exception) { }
            };

            var panel = new StackPanel { Margin = 8 };
            var grid = new Grid(11, 3);

            grid.Rows[1].SetHeightInPixels(2);
            grid.Rows[3].SetHeightInPixels(8);
            grid.Rows[5].SetHeightInPixels(8);
            grid.Rows[7].SetHeightInPixels(2);
            grid.Rows[9].SetHeightInPixels(8);

            grid.Columns[0].SetWidthInStars(1);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[2].SetWidthInStars(1);

            grid.AddChild(CreateLabel("Action Price"), 0, 0);
            grid.AddChild(actionPrice, 2, 0);
            grid.AddChild(submitActionPrice, 2, 2);

            grid.AddChild(useAvg, 4, 0);
            grid.AddChild(trailStop, 4, 2);

            grid.AddChild(CreateLabel("Stop Loss"), 6, 0);
            grid.AddChild(CreateLabel("Take Profit"), 6, 2);
            grid.AddChild(stopLoss, 8, 0);
            grid.AddChild(takeProfit, 8, 2);
            grid.AddChild(submitTpSl, 10, 2);

            grid.AddChild(ClearAllButton, 10, 0);

            panel.AddChild(grid);

            return panel;
        }

        private TextBlock CreateLabel(string label, TextAlignment textAlignment = TextAlignment.Left)
        {
            return new TextBlock
            {
                Text = label,
                TextAlignment = textAlignment,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}