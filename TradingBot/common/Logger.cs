using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    //
    // Summary:
    //     Main application logger.
    public class Logger
    {
        private readonly StreamWriter _file;
        private static Logger _instance;
        private string _fileName;
        private Logger()
        {
            _fileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".log";
            _file = new StreamWriter(_fileName, false);
            _file.AutoFlush = true;
        }

        public static void Write(string format, params object?[] args)
        {
            if (_instance == null)
                _instance = new Logger();

            var message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ff").PadRight(28);
            message += string.Format(format, args);
            _instance._file.WriteLine(message);
        }

        public static string FileName()
        {
            if (_instance == null)
                _instance = new Logger();

            return _instance._fileName;
        }
    }
}
