echo OFF
Setlocal EnableDelayedExpansion

rem INPUT VARIABLES
SET STRATEGY=MorningOpenStrategy
SET INPUT_FOLDER=..\TestData\RawQuotes\5m\uncompressed\
SET OUTPUT_FOLDER=.\\test\\
SET CPU_CORES=8

echo test
rem SCRIPT BODY
SET PROCESSED=0
FOR /f "tokens=*" %%G IN ('dir /b /s /a:d "%INPUT_FOLDER%"') DO (
	echo %%G
	start ..\TradingBot\bin\Release\netcoreapp3.1\TradingBot.exe mode="TestTradeBot" DumpQuotes=false SubscribeQuotes=false Strategies="%STRATEGY%" Token="token.txt" ConfigPath="usd.json" OutputFolder="%OUTPUT_FOLDER%" CandlesPath="%%G"
	SET /a PROCESSED+=1
	IF !PROCESSED!==%CPU_CORES% (
		timeout /T 20 /NOBREAK
		SET PROCESSED=0
	) ELSE (
		timeout /T 2 /NOBREAK)
)

IF %OUTPUT_FOLDER%=="" (
		start ..\TradingBot\bin\Release\netcoreapp3.1\TradingBot.exe mode="AnalyzeLogs" Token="token.txt" ConfigPath="config.json" SourceFolder="%OUTPUT_FOLDER%"
	) ELSE (


