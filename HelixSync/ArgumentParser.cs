// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Reflection;
using System.Linq;
using System.ComponentModel;

namespace HelixSync
{
    public class ArgumentParser
    {
        public static void ParseCommandLineArguments(object optionsObj, string[] args, int startingPosition = 0)
        {
            if (optionsObj == null)
                throw new ArgumentNullException(nameof(optionsObj));
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            Type optionsObjType = optionsObj.GetType();

            bool forcePositional = false;
            int pos = 0;
            for (int i = startingPosition; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--")
                {
                    forcePositional = true;

                }
                else if (forcePositional || !arg.StartsWith("-", StringComparison.OrdinalIgnoreCase))
                {
                    var positionalProperty = optionsObjType
                        .GetProperties()
                        .Where(p => p.GetCustomAttributes<CategoryAttribute>()
                               .Any(a => a.Category == "Position " + pos.ToString()))
                        .FirstOrDefault();

                    if (positionalProperty == null)
                        throw new ApplicationException("To many positional arguments");
                    positionalProperty.SetValue(optionsObj, arg);

                    pos++;
                }
                else {
                    string argName = arg.Substring(1);
                    string argOpt = "";
                    if (argName.Contains(":"))
                    {
                        argOpt = argName.Split(":".ToArray(), 2)[1];
                        argName = argName.Split(":".ToArray(), 2)[0];
                    }

                    PropertyInfo propertyInfo = optionsObjType
                        .GetProperties()
                        .Where(p => string.Equals(p.Name, argName,
                                                  StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    if (propertyInfo == null)
                        throw new ApplicationException("Invalid Option " + arg);

                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        if (!string.IsNullOrEmpty(argOpt))
                            throw new ApplicationException("Invalid Option " + arg);

                        if (args.Length <= i + 1)
                            throw new ApplicationException("Expecting a value after the argument " + arg);

                        i++;
                        var val = args[i];
                        propertyInfo.SetValue(optionsObj, val);
                    }
                    else if (propertyInfo.PropertyType == typeof(bool))
                    {
                        if (string.IsNullOrEmpty(argOpt))
                            propertyInfo.SetValue(optionsObj, true);
                        else
                        {
                            bool valueOut;
                            if (!bool.TryParse(argOpt, out valueOut))
                                throw new ApplicationException("Invalid Argument Option (" + argOpt + " expecting -" + argName + ":true or -" + argName + ":false");
                            propertyInfo.SetValue(optionsObj, valueOut);
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(string[]))
                    {
                        if (!string.IsNullOrEmpty(argOpt))
                            throw new ApplicationException("Invalid Option " + arg);
                        if (args.Length <= i + 1)
                            throw new ApplicationException("Expecting a value after the argument " + arg);

                        i++;
                        string val = args[i];
                        string[] originalValue = (string[])propertyInfo.GetValue(optionsObj);
                        string[] newValue = (originalValue ?? new string[] { }).Concat(new string[] { val }).ToArray();
                        propertyInfo.SetValue(optionsObj, newValue);
                    }
                    else 
                    {
                        throw new ApplicationException("Unsupported Option " + arg);
                    }

                }
            }
        }
    }
}
