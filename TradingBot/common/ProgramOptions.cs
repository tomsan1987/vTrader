using System;
using System.Collections.Generic;
using System.Text;

namespace TradingBot
{
    public class ProgramOptions
    {
        private Dictionary<string, string> _options;

        public ProgramOptions(string[] args)
        {
            _options = new Dictionary<string, string>();

            foreach (var it in args)
            {
                var result = it.Split("=", StringSplitOptions.RemoveEmptyEntries);
                if (result.Length == 2)
                    _options.Add(result[0], result[1]);
            }
        }

        public T Get<T>(string name)
        {
            string value;
            if (_options.TryGetValue(name, out value))
                return (T)Convert.ChangeType(value, typeof(T));

            return default(T);
        }

        public bool HasValue(string name)
        {
            string value;
            return _options.TryGetValue(name, out value);
        }

        public override string ToString()
        {
            string res = "";
            foreach (var it in _options)
                res += it.Key + "=" + it.Value + ";";

            return res;
        }
    }
}
