echo OFF
Setlocal EnableDelayedExpansion

rem INPUT VARIABLES
SET STRATEGY=MorningOpenStrategy
SET INPUT_FOLDER=C:\tinkoff\Configuration\test
start /min ..\TradingBot\bin\Release\netcoreapp3.1\TradingBot.exe mode="TestTradeBot" DumpQuotes=false SubscribeQuotes=false Strategies="%STRATEGY%" Token="token.txt" ConfigPath="usd.json" CandlesPath="%INPUT_FOLDER%"
