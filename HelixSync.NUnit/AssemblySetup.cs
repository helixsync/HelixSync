// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace HelixSync.NUnit
{
    [SetUpFixture]
    public class AssemblySetup
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}
