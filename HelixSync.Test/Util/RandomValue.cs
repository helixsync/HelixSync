// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

#if FSCHECK
using FsCheck;
using static FsCheck.Random;


namespace HelixSync.Test
{
    class RandomValue
    {
        static System.Random random = new System.Random();
        static int testID = -1;
        static int numberID = 0;

        public static void NewTest(int testID = -1)
        {
            RandomValue.testID = testID != -1 ? testID : random.Next();
            numberID = 0;
            Console.WriteLine("Test ID: {0}", RandomValue.testID);
        }

        public static StdGen NextRandomGenerator()
        {
            if (testID == -1)
                NewTest();

            return FsCheck.Random.StdGen.NewStdGen(testID, numberID++);
        }

        public static T GetValue<T>(Predicate<T> predicate,int size=10)
        {
            while (true)
            {
                var val = GetValue<T>(size);
                if (predicate(val))
                    return val;
            }
        }

        public static T GetValue<T>(int size=10)
        {
            var arb = FsCheck.Arb.From<T>();
            return arb.Generator.Eval<T>(10, NextRandomGenerator());
        }

        public static string GetFileName()
        {
            var value = GetValue<string>((s) => !string.IsNullOrWhiteSpace(s));
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c.ToString(), "_");
            }

            return value;
        }
        
        public static string[] GetDirectoryStructure(int size = 20)
        {
            List<string> content = new List<string>();
            string lastDirectory = "";
            var iterations = GetValue<byte>() % size;
            for(int i = 0; i < iterations; i++)
            {
                var choose = GetValue<byte>();
                if (choose % 3 == 0)
                {
                    //Add File
                    content.Add(Path.Combine(lastDirectory, GetFileName()));
                }
                else if (choose % 3 == 1)
                {
                    //Add subdirectory
                    lastDirectory = Path.Combine(lastDirectory, GetFileName());
                    content.Add(lastDirectory + Path.DirectorySeparatorChar);
                }
                else if (choose % 3 == 2)
                {
                    //Navigate Up
                    if (!string.IsNullOrEmpty(lastDirectory))
                        lastDirectory = Path.GetDirectoryName(lastDirectory);
                }
            }
            return content.ToArray();
        }

        public static int GetInt32()
        {
            return GetValue<Int32>();
        }

        public static T[] ChooseMany<T>(T[] Array, int maxCount = 10)
        {
            if (Array.Length == 0)
                return new T[0];

            List<T> toBeChosen = new List<T>(Array);
            List<T> outputList = new List<T>();
            var count = Math.Abs(GetInt32()) % toBeChosen.Count;
            for(int i= 0; i < count; i++)
            {
                var chosen = Math.Abs(GetInt32()) % toBeChosen.Count;
                outputList.Add(toBeChosen[chosen]);
                toBeChosen.RemoveAt(chosen);
            }
            return outputList.ToArray();
        }

        public static string GetString(Predicate<string> predicate) 
        {
            return GetValue<string>(predicate);
        }

        public static string GetString(RandomValueOptions options = RandomValueOptions.Default)
        {
            Predicate<string> predicate = (s) => true;
            
            if (options.HasFlag(RandomValueOptions.NotNull))
            {
                var p1 = predicate;
                predicate = (s) => p1(s) && s != null; 
            }

            if (options.HasFlag(RandomValueOptions.NotEmpty))
            {
                var p1 = predicate;
                predicate = (s) => p1(s) && s != string.Empty; 
            }

            return GetValue<string>(predicate);
        }
    }
}
#endif