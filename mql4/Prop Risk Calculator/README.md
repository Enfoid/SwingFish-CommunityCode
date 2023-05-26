0.97.2
SwingFish Prop Risk Indicator
 
Details https://swingfish.trade/mt4-indicator-prop-risk

displays realtime Risk for the next position based on Drawdown Limits for Prop accounts.

ToDo / Issues
- Indices have wrong calculations (about 10x)
- indicator does not reset when positions closed - need to reload the template to reset
- remove nonsense and rename Fields

Known Bugs
- items do not reset after all positions closed (requires to reload the indicator or change the symbol to make it work again)


Change log:
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
