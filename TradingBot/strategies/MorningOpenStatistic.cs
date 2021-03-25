using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Tinkoff.Trading.OpenApi.Models;
using Newtonsoft.Json;

namespace TradingBot
{
    public class MorningOpenStatistic
    {
        // Analyze history data by some criteria and create output file with statistic
        // Param: candlesPath - path to folder with candles in csv format. Iterating with sub folders
        // Param: outputFolder - path to folder for store results
        public static void CreateStatisticByHistoryData(string candlesPath, string outputFolder)
        {
            var writer = new StreamWriter(outputFolder + "\\stat_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv", false);
            writer.AutoFlush = true;
            writer.WriteLine("Ticker;FileName;CountQuotes;PrevDayClose;TodayOpen;TodayClose;TodayMin;TodayMax;ChangeMinToClose;ChangeMinToOpen;ChangeOpenClose");

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
                var fileName = Path.GetFileNameWithoutExtension(f.FullName);
                var ticker = f.Name.Substring(0, f.Name.IndexOf("_"));

                if (list.Count < 2)
                    continue;

                // check if we have previous day candle
                int currDayStartPos = 0;
                while (currDayStartPos < list.Count && list[currDayStartPos].Time.Hour > 20)
                {
                    ++currDayStartPos; // skip all candles from previous day
                }

                if (currDayStartPos >= list.Count)
                    continue;

                decimal prevDayClose = 0.0m;
                if (currDayStartPos > 0)
                    prevDayClose = list[currDayStartPos - 1].Close; // else we just do not know close of previous day

                var firstCandle = list[currDayStartPos];
                decimal todayMin = firstCandle.Close;
                decimal todayMax = firstCandle.Close;

                int i = currDayStartPos;
                while (i < list.Count && list[i].Time == firstCandle.Time)
                {
                    todayMin = Math.Min(todayMin, list[i].Close);
                    todayMax = Math.Max(todayMax, list[i].Close);
                    ++i;
                }

                decimal volume = list[i - 1].Volume;
                decimal todayClose = list[i - 1].Close;
                int countQuotes = i - currDayStartPos;
                var changeMinToClose = Helpers.GetChangeInPercent(todayMin, todayClose);
                var changeMinToOpen = Helpers.GetChangeInPercent(todayMin, firstCandle.Close);
                var changeOpenClose = Helpers.GetChangeInPercent(firstCandle.Close, todayClose);

                if (volume > 100 && countQuotes > 10 && ((changeMinToClose > 1m && changeMinToOpen > 1m) || (changeOpenClose < -2m)))
                {
                    // so we have some metrics, lets output it to log file
                    writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}", ticker, fileName, countQuotes, prevDayClose, firstCandle.Close, todayClose, todayMin, todayMax, changeMinToClose, changeMinToOpen, changeOpenClose);

                    // copy file if output folder specified
                    try
                    {
                        if (outputFolder.Length > 0)
                            File.Copy(f.FullName, outputFolder + "\\" + f.Name);
                    }
                    catch (Exception e)
                    {
                        Logger.Write("Exception happened while copying file. Error: " + e.Message);
                    }
                }
            }

            writer.Close();
        }
    }
}
