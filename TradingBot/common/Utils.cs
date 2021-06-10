using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tinkoff.Trading.OpenApi.Models;
using Tinkoff.Trading.OpenApi.Network;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Linq;

namespace TradingBot
{
    public static class Utils
    {
        // Read candles for each instrument and create daily statistic: price change for Open to Close and Low to High
        // Param: candlesPath - path to folder with raw candles data in csv format
        // Results will save to a file "_stat.txt"
        public static void CreateCandlesStatistic(string candlesPath)
        {
            List<Screener.Stat> openToClose = new List<Screener.Stat>();
            List<Screener.Stat> lowToHigh = new List<Screener.Stat>();

            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var candleList = TradeBot.ReadCandles(f.FullName);
                if (candleList.Count > 0)
                {
                    decimal open = candleList[0].Open;
                    decimal close = candleList[0].Close;
                    decimal low = candleList[0].Low;
                    decimal high = candleList[0].High;
                    var lowTime = candleList[0].Time;
                    var highTime = candleList[0].Time;

                    for (int i = 1; i < candleList.Count; ++i)
                    {
                        if (candleList[i].Low <= low)
                        {
                            low = candleList[i].Low;
                            lowTime = candleList[i].Time;
                        }

                        if (candleList[i].High >= high)
                        {
                            high = candleList[i].High;
                            highTime = candleList[i].Time;
                        }
                    }

                    close = candleList[candleList.Count - 1].Close;

                    var fileName = Path.GetFileNameWithoutExtension(f.FullName);
                    var fileParts = fileName.Split("_");
                    if (fileParts.Length < 3)
                        throw new Exception("Wrong file name format with candles");

                    var ticker = fileParts[0];
                    openToClose.Add(new Screener.Stat(ticker, open, close));

                    if (highTime >= lowTime)
                        lowToHigh.Add(new Screener.Stat(ticker, low, high));
                    else
                        lowToHigh.Add(new Screener.Stat(ticker, high, low));
                }
            }

            openToClose.Sort();
            lowToHigh.Sort();

            // create output file
            var file = new StreamWriter(candlesPath + "\\_stat.txt", false);
            file.AutoFlush = true;

            const int cMaxOutput = 15;
            Action<string, List<Screener.Stat>> showStat = (message, list) =>
            {
                file.WriteLine(message);
                var j = list.Count - 1;
                for (var i = 0; i < Math.Min(list.Count, cMaxOutput); ++i)
                {
                    String lOutput = String.Format("{0}:", list[i].ticker).PadLeft(7);
                    lOutput += String.Format("{0}%", list[i].change).PadLeft(7);
                    lOutput += String.Format("({0} ->> {1})", list[i].open, list[i].close).PadLeft(20);
                    lOutput = lOutput.PadRight(10);

                    file.Write(lOutput);

                    if (j >= cMaxOutput)
                    {
                        String rOutput = String.Format("{0}:", list[j].ticker).PadLeft(7);
                        rOutput += String.Format("{0}%", list[j].change).PadLeft(7);
                        rOutput += String.Format("({0} ->> {1})", list[j].open, list[j].close).PadLeft(20);

                        file.Write(rOutput);
                        --j;
                    }

                    file.Write("\n");
                }
            };

            showStat("Open to Close statistics:", openToClose);
            showStat("Low to High statistics:", lowToHigh);

            file.Close();
        }

        // When Screener was restarted at the middle of the day - CandleID started from zero
        // Method re-enumerates CandleIDs for whole file
        // Param: candlesPath - path to folder with raw candles data in csv format
        // NOTE: Results will save to the same files!
        public static void CorrectCandleID(string candlesPath)
        {
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                var array = File.ReadAllLines(f.FullName);
                if (array.Length == 0)
                    continue; // do nothing for empty files

                var file = new StreamWriter(f.FullName, false);
                file.AutoFlush = true;
                file.WriteLine(array[0]); // header

                for (int i = 1; i < array.Length; ++i)
                {
                    var line = array[i];
                    var idx = line.IndexOf(';');
                    if (idx > 0)
                        line = (i - 1).ToString() + ";" + line.Substring(idx + 1);

                    file.WriteLine(line);
                }

                file.Close();
            }
        }

        // Parses a collection of trade log files in a specified folder and creates common trade statistic in a result file
        // Param: sourceFolder - path to folder with trade log files(*.log). When empty - current working directory will used.
        // Result will save .log file in current working directory
        public static void AnalyzeLogs(string sourceFolder)
        {
            bool ok = false;
            TradeStatistic stat = new TradeStatistic();

            if (sourceFolder.Length == 0)
                sourceFolder = System.IO.Directory.GetCurrentDirectory();

            DirectoryInfo folder = new DirectoryInfo(sourceFolder);
            foreach (FileInfo f in folder.GetFiles("*.log"))
            {
                ok = false;
                var lastStr = String.Empty;
                var lines = File.ReadLines(f.FullName).GetEnumerator();
                while (lines.MoveNext())
                {
                    if (lines.Current.IndexOf("Trade statistic") >= 0)
                    {
                        lastStr = lines.Current;
                        break;
                    }
                }

                if (lastStr.Length > 0)
                {
                    //Trade statistic. Total/Pos/Neg 42/16/26. Profit: -6,46. Volume: 3120,24. MaxVolume: 1704,93.Commission: 3,117010
                    //Trade statistic. Total/Pos/Neg/(ratio): 3/1/2/(0.5); Profit: -0.77/-0.26; Volume: 0; MaxVolume: 223.51; Commission: 0.665945;
                    for (;;)
                    {
                        int start = 0;
                        int end = 0;

                        start = lastStr.IndexOf("Total/Pos/Neg/(ratio): ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 23, end - (start + 23));
                                var array = str.Split("/");
                                if (array.Length >= 3)
                                {
                                    stat.totalOrders += int.Parse(array[0]);
                                    stat.posOrders += int.Parse(array[1]);
                                    stat.negOrders += int.Parse(array[2]);
                                }
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("Profit: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf('/', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.totalProfit += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("Volume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 8, end - (start + 8));
                                stat.volume += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        start = lastStr.IndexOf("MaxVolume: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 11, end - (start + 11));
                                stat.maxVolume = Math.Max(stat.maxVolume, Helpers.Parse(str));
                            }
                        }
                        else
                            break; // error parse


                        start = lastStr.IndexOf("Commission: ");
                        if (start > 0)
                        {
                            end = lastStr.IndexOf(';', start);
                            if (end > start)
                            {
                                var str = lastStr.Substring(start + 12, end - (start + 12));
                                stat.comission += Helpers.Parse(str);
                            }
                        }
                        else
                            break; // error parse

                        ok = true;
                        break;
                    }

                    if (!ok)
                        Logger.Write("Error getting stats for {0}. Line: {1}", f.FullName, lastStr);
                }
            }

            if (stat.totalOrders > 0)
                Logger.Write(stat.GetStringStat());
        }

        // Convert quotes from json to csv format
        // Param: candlesPath - path to folder with candles in json format
        public static void ConvertQuotes(string candlesPath)
        {
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.json"))
            {
                // convert json quotes to simple csv data
                var list = TradeBot.ReadCandles(f.FullName);
                if (list.Count == 0)
                {
                    Logger.Write("No data found for " + f.FullName);
                    continue;
                }

                var figi = list[list.Count / 2].Figi;
                var date = list[list.Count / 2].Time.ToString("_yyyy-MM-dd");
                var fileName = Path.GetFileNameWithoutExtension(f.FullName) + "_" + figi + date;
                var dir = Path.GetDirectoryName(f.FullName);

                var outputName = dir + "\\" + fileName + ".csv";
                var file = new StreamWriter(outputName, false);
                file.AutoFlush = true;

                List<Quotes.Quote> raw = new List<Quotes.Quote>();
                List<Trend> trends = new List<Trend>();

                // write header
                file.WriteLine("#;Time;Open;Close;Low;High;Volume");
                for (int i = 0; i < list.Count; ++i)
                {
                    var candle = list[i];
                    raw.Add(new Quotes.Quote(candle.Close, candle.Volume, candle.Time));

                    string message = String.Format("{0};{1};{2};{3};{4};{5};{6}", i, candle.Time.ToShortTimeString(), candle.Open, candle.Close, candle.Low, candle.High, candle.Volume);
                    message.Replace('.', ',');
                    file.WriteLine(message);
                }

                file.Close();
            }
        }

        // Select history data by some criteria
        // Param: candlesPath - path to folder with candles in csv format. Iterating with sub folders
        public static void SelectHistoryData(string candlesPath, string outputFolder)
        {
            var writer = new StreamWriter(outputFolder + "\\stat_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv", false);
            writer.AutoFlush = true;
            writer.WriteLine("Ticker;FileName;Time;CountQuotes;changeOpenToLow;changeLowToClose");

            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv", SearchOption.AllDirectories))
            {
                var list = TradeBot.ReadCandles(f.FullName);
                if (list.Count == 0)
                {
                    Logger.Write("No data found for " + f.FullName);
                    continue;
                }

                var figi = list[list.Count / 2].Figi;
                var date = list[list.Count / 2].Time.ToString("_yyyy-MM-dd");
                var fileName = Path.GetFileNameWithoutExtension(f.FullName) + "_" + figi + date;
                var ticker = f.Name.Substring(0, f.Name.IndexOf("_"));

                if (list.Count < 1)
                    continue;

                // skip 2 candles after market open
                int i = 1;
                int candlesSkipped = 0;
                var prevCandle = list[0];
                while (i < list.Count && candlesSkipped < 2)
                {
                    if (prevCandle.Time != list[i].Time)
                    {
                        ++candlesSkipped;
                        prevCandle = list[i];
                    }

                    ++i;
                }

                while (i < list.Count)
                {
                    // form-up full candle
                    var startPos = i;
                    while (i < list.Count && prevCandle.Time == list[i].Time)
                    {
                        prevCandle.Close = list[i].Close;
                        prevCandle.High = Math.Max(prevCandle.High, list[i].Close);
                        prevCandle.Low = Math.Min(prevCandle.Low, list[i].Close);
                        ++i;
                    }

                    // premarket and quotes count limit
                    if (prevCandle.Time.Hour < 13 && i - startPos > 10)
                    {
                        var changeOpenToLow = Helpers.GetChangeInPercent(prevCandle.Open, prevCandle.Low);
                        var changeLowToClose = Helpers.GetChangeInPercent(prevCandle.Low, prevCandle.Close);

                        if (changeOpenToLow <= -2m)
                        {
                            // Ticker; FileName; Time; CountQuotes; changeOpenToLow; changeLowToClose
                            writer.WriteLine("{0};{1};{2};{3};{4};{5}", ticker, fileName, prevCandle.Time.ToShortTimeString(), i - startPos, changeOpenToLow, changeLowToClose);
                        }
                    }

                    if (i < list.Count && prevCandle.Time != list[i].Time)
                    {
                        prevCandle = list[i];
                    }

                    ++i;
                }
            }

            writer.Close();
        }

        public async static Task CreateWatchList(BaseBot.Settings settings, ProgramOptions po)
        {
            var connection = ConnectionFactory.GetConnection(settings.Token);
            var context = connection.Context;

            string _accountId = "";
            var accounts = await context.AccountsAsync();
            foreach (var acc in accounts)
            {
                if (acc.BrokerAccountType == BrokerAccountType.Tinkoff)
                    _accountId = acc.BrokerAccountId;
            }

            var instruments = (await context.MarketStocksAsync()).Instruments;

            Action<Currency, decimal, decimal, decimal> store = (c, minPrice, maxPrice, minVolume) =>
            {
                int idx = 0;
                List<string> list = new List<string>();
                foreach (var it in instruments)
                {
                    if (it.Currency == c)
                    {
                        if (minPrice > 0 || maxPrice > 0)
                        {
                            decimal price = 0;
                            decimal volume = 0;
                            bool ok = false;
                            while (!ok)
                            {
                                try
                                {
                                    ++idx;
                                    var task = context.MarketCandlesAsync(it.Figi, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow, CandleInterval.Month);
                                    task.Wait();
                                    var candles = task.Result.Candles;
                                    if (candles.Count > 0)
                                    {
                                        price = candles[candles.Count - 1].Close;
                                        foreach (var candle in candles)
                                            volume += candle.Volume;

                                        volume /= candles.Count;
                                    }

                                    ok = true;
                                }
                                catch (Exception)
                                {
                                    Logger.Write("Context: waiting after {0} queries....", idx);
                                    ok = false;
                                    idx = 0;
                                    Task.Delay(30000).Wait(); // sleep for a while
                                }
                            }

                            if (price > 0)
                            {
                                var minPriceOk = (price >= minPrice);
                                var maxPriceOk = (maxPrice == 0 || price <= maxPrice);
                                var minVoulmeOk = (minVolume == 0 || volume > minVolume);
                                if (minPriceOk && maxPriceOk && minVoulmeOk)
                                    list.Add(it.Ticker);
                            }
                            else
                            {
                                Logger.Write("Can't get price for {0}. Instrument will be skipped...", it.Ticker);
                            }
                        }
                        else
                            list.Add(it.Ticker);
                    }
                }

                list.Sort();

                JArray array = new JArray();
                foreach (var ticker in list)
                    array.Add(ticker);

                JObject json = new JObject();
                json["watch-list"] = array;

                var file = new StreamWriter(c.ToString() + ".json", false);
                file.AutoFlush = true;
                file.Write(json.ToString());
                file.Close();
            };

            Currency currency = (Currency)Enum.Parse(typeof(Currency), po.Get<string>("Currency"), true);
            var minPrice = po.Get<decimal>("MinPrice");
            var maxPrice = po.Get<decimal>("MaxPrice");
            var minVolume = po.Get<decimal>("MinVolume");

            store(currency, minPrice, maxPrice, minVolume);
        }

        // Correct history data:
        //      - remove previous day close data if it repeated some times at the beginning
        //      - re-numerate candles 0...n
        //      - remove wrong stored data from the next day
        // Param: CandlesPath - path to folder with candles in csv format. Iterating with sub folders
        //        OutputFolder[optional] - path to store result data
        public static void CorrectHistoryData(string candlesPath, string outputFolder)
        {
            DirectoryInfo folder = new DirectoryInfo(candlesPath);
            foreach (FileInfo f in folder.GetFiles("*.csv"))
            {
                if (outputFolder.Length > 0 && !Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                var fileStream = File.OpenRead(f.FullName);
                var streamReader = new StreamReader(fileStream);

                var line = streamReader.ReadLine(); // header
                var values = line.Split(";");
                if (values.Length < 7)
                    throw new Exception("Wrong file format with candles");

                DateTime prevTime = DateTime.Now;

                var outputName = outputFolder + "\\" + f.Name;
                var file = new StreamWriter(outputName, false);
                file.AutoFlush = true;
                file.WriteLine(line); // header

                // read first 10 candles to define if there a candle of previous day close
                List<string> buffer = new List<string>();
                while (buffer.Count < 10 && (line = streamReader.ReadLine()) != null)
                    buffer.Add(line);

                int candleID = 0;

                // check for previous day close candle
                if (buffer.Count > 0)
                {
                    values = buffer[0].Split(";");
                    DateTime firstLine = DateTime.Parse(values[1]);

                    int startPos = 0;
                    for (int i = 1; i < buffer.Count; ++i)
                    {
                        values = buffer[i].Split(";");
                        DateTime current = DateTime.Parse(values[1]);
                        if (firstLine > current)
                        {
                            startPos = i - 1;
                            break;
                        }
                    }

                    // write lines starting from previous day close if exists
                    for (int i = startPos; i < buffer.Count; ++i)
                    {
                        line = candleID.ToString() + buffer[i].Substring(buffer[i].IndexOf(";"));
                        file.WriteLine(line);
                        ++candleID;
                    }

                    values = buffer[buffer.Count - 1].Split(";");
                    prevTime = DateTime.Parse(values[1]);
                }

                while ((line = streamReader.ReadLine()) != null)
                {
                    values = line.Split(";");
                    var currentTime = DateTime.Parse(values[1]);

                    if (currentTime.AddMinutes(5) < prevTime)
                        break; // finished

                    prevTime = currentTime;

                    line = candleID.ToString() + line.Substring(line.IndexOf(";"));
                    file.WriteLine(line);
                    ++candleID;
                }

                file.Close();
                streamReader.Close();
                fileStream.Close();
            }
        }
        public static string ToEnumString<T>(T type)
        {
            var enumType = typeof(T);
            var name = Enum.GetName(enumType, type);
            var enumMemberAttribute = ((EnumMemberAttribute[])enumType.GetField(name).GetCustomAttributes(typeof(EnumMemberAttribute), true)).Single();
            return enumMemberAttribute.Value;
        }
    }
}
