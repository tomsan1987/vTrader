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
        private Logger()
        {
            _file = new StreamWriter("app_log.log", true);
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
    }
}
