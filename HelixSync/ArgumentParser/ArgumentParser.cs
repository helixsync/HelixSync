// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Reflection;
using System.Linq;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Collections.Generic;

namespace HelixSync
{
    public class ArgumentParser
    {
        private class ArgumentMetadata
        {
            private PropertyInfo propertyInfo;

            public ArgumentMetadata(PropertyInfo propertyInfo)
            {
                this.propertyInfo = propertyInfo;
            }

            public int OrdinalPosition
            {
                get
                {
                    var position = propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.OrdinalPosition ?? int.MaxValue;
                    if (position < 0) position = int.MaxValue;
                    return position;
                }
            }
            public bool IsOrdinal => OrdinalPosition != int.MaxValue;


            public string Name
            {
                get
                {
                    return propertyInfo.Name;
                }
            }

            public bool Required => propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.Required == true;

            public bool Recommended => Required || OrdinalPosition != int.MaxValue || propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.Recommended == true;

            public bool PreferShortName => propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.PreferShortName == true;

            public string ShortName => propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.ShortName;

            public bool IsBool => propertyInfo.PropertyType == typeof(Boolean);

            public string ValuePlaceholder => propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.ValuePlaceholder ?? "value";


            public string UsageString(bool? shortName = null, bool dualPattern = false) 
            {
                if (dualPattern && !string.IsNullOrEmpty(this.ShortName))
                {
                    return UsageString(PreferShortName) + " | " + UsageString(!PreferShortName);
                }


                shortName = shortName ??  PreferShortName;
                string usageString = "";
                if (shortName == true)
                    usageString += $"-{ShortName}";
                else
                    usageString += $"-{Name}";

                if (IsBool && Required)
                    usageString += ":<True|False>";
                else if (IsBool)
                    usageString += "";
                else
                    usageString += $" \"{ValuePlaceholder}\"";

                return usageString;
            }


            public string Description => propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;

            public bool Advanced => propertyInfo.GetCustomAttribute<ArgumentAttribute>()?.Advanced == true;

            public override string ToString()
            {
                if (OrdinalPosition > 0 && OrdinalPosition != int.MaxValue)
                    return $"[{Name}] \"{ValuePlaceholder}";
                else
                    return $"{Name} \"{ValuePlaceholder}";
            }
        }


        public static string UsageString(Type type, string begining, bool advanced)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("= Usage =");
            sb.AppendLine(UsageLine(type, begining));
            sb.AppendLine();
            sb.AppendLine("= Options =");
            sb.AppendLine(UsageOptions(type, advanced));
            sb.AppendLine();
            if(advanced) {
                sb.AppendLine("= Advanced =");
                sb.AppendLine(UsageAdvanced(type));
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static string UsageLine(Type type, string begining)
        {
            var args = type.GetProperties().Select(p => new ArgumentMetadata(p))
                .OrderBy(p => p.OrdinalPosition)
                .ThenBy(p => p.Required)
                .ThenBy(p => p.Recommended)
                .ThenBy(p => p.Name)
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.Append(begining + " ");
            HashSet<ArgumentMetadata> visitedArgs = new HashSet<ArgumentMetadata>();

            foreach (var arg in args.Where(a => a.Required && !a.IsOrdinal))
            {
                sb.Append(arg.UsageString() + " ");
                visitedArgs.Add(arg);
            }

            foreach (var arg in args.Where(a => a.Recommended && !a.IsOrdinal && !visitedArgs.Contains(a)))
            {
                sb.Append("[" + arg.UsageString() + "] ");
                visitedArgs.Add(arg);
            }

            if (args.Any(a => !a.Required && !a.Recommended && !a.IsOrdinal))
            {
                if (visitedArgs.Any())
                    sb.Append("[OtherOptions] ");
                else
                    sb.Append("[Options] ");
            }

            bool notFirst = false;
            int nesting = 0;
            foreach (var arg in args.Where(a => a.IsOrdinal && !visitedArgs.Contains(a)))
            {
                if (notFirst)
                    sb.Append(" ");

                if (!arg.Required)
                {
                    sb.Append("[");
                    nesting++;
                }
                notFirst = true;

                sb.Append($"\"{arg.Name}\"");
                visitedArgs.Add(arg);
            }
            sb.Append(new string(']', nesting));

            return sb.ToString().Trim();
        }

        public static string UsageOptions(Type type, bool advanced)
        {
            var args = type.GetProperties().Select(p => new ArgumentMetadata(p))
                .OrderBy(p => p.OrdinalPosition)
                .ThenBy(p => p.Required)
                .ThenBy(p => p.Recommended)
                .ThenBy(p => p.Name)
                .ToList();

            StringBuilder sb = new StringBuilder();
            foreach (var arg in args.Where(p => advanced || !p.Advanced))
            {
                string flags = "";
                if (arg.IsOrdinal)
                    sb.AppendLine($"\"{arg.Name}\"");
                else
                    sb.AppendLine($"{arg.UsageString(null, true)}");

                if (arg.IsBool)
                    flags += "[bool]";

                if (!string.IsNullOrEmpty(flags))
                    flags += " ";

                sb.AppendLine(flags + (arg.Description ?? "No Description Available"));
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        public static string UsageAdvanced(Type type)
        {
            return "Multivalued parameters can be repeated (-name \"val1\" -name \"val2\")\n" +
                "Use double dash (--) to end command options and begin positional parammeters\n" +
                "Bool parameters can be postfix with --switch:true or --switch:false";
        }


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
                        .Where(p => p.GetCustomAttributes<ArgumentAttribute>()
                               .Any(a => a.OrdinalPosition == pos))
                        .FirstOrDefault();

                    if (positionalProperty == null)
                        throw new ArgumentParseException($"Too many positional arguments provided ({pos})");
                    positionalProperty.SetValue(optionsObj, arg);

                    pos++;
                }
                else
                {
                    string argName;
                    if (arg.StartsWith("--"))
                        argName = arg.Substring(2);
                    else
                        argName = arg.Substring(1);

                    //--ArgName:False syntax can be used with boolean flags
                    string argOpt = "";
                    if (argName.Contains(":"))
                    {
                        argOpt = argName.Split(":".ToArray(), 2)[1];
                        argName = argName.Split(":".ToArray(), 2)[0];
                    }

                    PropertyInfo propertyInfo = optionsObjType
                        .GetProperties()
                        .Where(p => string.Equals(argName, p.Name, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(argName,
                                                 p.GetCustomAttribute<ArgumentAttribute>()?.ShortName,
                                                 StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    if (propertyInfo == null)
                        throw new ArgumentParseException("Unrecognised Option " + arg);

                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        if (!string.IsNullOrEmpty(argOpt))
                            throw new ArgumentParseException("Invalid Option " + arg + ", value is required");

                        if (args.Length <= i + 1)
                            throw new ArgumentParseException("Expecting a value after the argument " + arg);

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
                                throw new ArgumentParseException("Invalid Argument Option (" + argOpt + " expecting -" + argName + ":true or -" + argName + ":false");
                            propertyInfo.SetValue(optionsObj, valueOut);
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(string[]))
                    {
                        if (!string.IsNullOrEmpty(argOpt))
                            throw new ArgumentParseException("Invalid Option " + arg);
                        if (args.Length <= i + 1)
                            throw new ArgumentParseException("Expecting a value after the argument " + arg);

                        i++;
                        string val = args[i];
                        string[] originalValue = (string[])propertyInfo.GetValue(optionsObj);
                        string[] newValue = (originalValue ?? new string[] { }).Concat(new string[] { val }).ToArray();
                        propertyInfo.SetValue(optionsObj, newValue);
                    }
                    else
                    {
                        throw new ArgumentParseException("Unsupported Option " + arg);
                    }

                }
            }
        }
    }
}
