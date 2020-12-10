using System.Collections.Generic;
using System.Text;

namespace IngameScript
{
    partial class Program
    {
        abstract class Options
        {
            public string getSaveString()
            {
                StringBuilder builder = new StringBuilder();
                Dictionary<string, object> values = getValues();
                foreach (KeyValuePair<string, object> kv in values.Values)
                {
                    builder.Append(kv.Key);
                    builder.Append("=");
                    builder.Append(kv.Value);
                    builder.Append(" ");
                }

                return builder.ToString();
            }

            public abstract Dictionary<string, object> getValues();

            public void parse(string str)
            {
                parse(str.Split(' '), 0);
            }

            public void parse(string[] parts, int offset)
            {
                setDefaults();

                for (int i = offset; i < parts.Length; ++i)
                {
                    string arg = parts[i];
                    parseArg(arg);
                }
            }

            public abstract void setDefaults();
            public abstract void parseArg(string arg);

            private static float parseFloatArg(string arg, string argName)
            {
                return float.Parse(parseKvPairArg(arg, argName).Value);
            }

            private static int parseIntArg(string arg, string argName)
            {
                return int.Parse(parseKvPairArg(arg, argName).Value);
            }

            private static KeyValuePair<string, string> parseKvPairArg(string arg, string argName)
            {
                int equalsPos = arg.IndexOf('=');
                if (equalsPos == -1)
                {
                    throw new System.ArgumentException("Invalid format for argument " + argName + ": expected '='");
                }

                string key = arg.Substring(0, equalsPos);
                string value = arg.Substring(equalsPos + 1);
                return new KeyValuePair<string, string>(key, value);
            }
        }
    }
}