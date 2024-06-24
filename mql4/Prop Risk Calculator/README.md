0.98.6
SwingFish Prop Risk Indicator
 
Details https://swingfish.trade/mt4-indicator-prop-risk

displays realtime Risk for the next position based on Drawdown Limits for Prop accounts.

ToDo
- remove nonsense and rename Fields

Known Bugs
- when indicator is loaded during startup of MT4 it doesn't initialize properly
  Workaround: change the symbol or re-load the template 
- Indices have wrong calculations (about 10x)


Change log:
- add Risk on open positions next to RR value
- fix item reset
- added colors to indicate different risk status
- added Risk-reward to show when position in profit
- removed Currency Symbol for display
- removed "hide all" (just unload the indicator instead)
- remove "Hide_all" setting
- add active Risk/reward ratio (RR)
- replace "P/L" with "Open Risk" (shows the current exposure from open positions)
- variable Fix under OSX
- Next-Risk re-adjusts to open positions
- cleanup math (clean split between in-profit and base values)
- "Next Risk" includes now active positions (does not work on OSX)
- general cleanup
- add Percentage display for better Visualisation
- change "Profit" to "P/L"
- add add separate risk for profits and base equity
- rename to Prop Risk Calculator
- added position settings for individual outputs
- Minimum Equity as parameter
