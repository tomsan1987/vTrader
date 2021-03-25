SET STRATEGY=MorningOpenStrategy
SET OUTPUT_FOLDER=

start ..\TradingBot\bin\Release\netcoreapp3.1\TradingBot.exe mode="TestTradeBot" DumpQuotes=false SubscribeQuotes=false Strategies="%STRATEGY%" Token="token.txt" ConfigPath="usd.json" OutputFolder="%OUTPUT_FOLDER%" CandlesPath="C:\tinkoff\Tests\MorningOpenStrategy\Data2"
timeout /T 2 /NOBREAK                                                                                                                                                                                                                                                                          