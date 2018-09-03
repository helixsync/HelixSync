using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class ArgumentParse_Test
    {

        public class ExampleObject 
        {
            [Argument(OrdinalPosition = 0)]
            public string Value1 { get; set; }

            [Argument(OrdinalPosition = 1)]
            public string Value2 { get; set; }

            [Argument]
            public string Value3 { get; set; }
        }

        [TestMethod]
        public void ParseCommandLineArguments_OrdinalPositions()
        {
            var obj = new ExampleObject();
            ArgumentParser.ParseCommandLineArguments(obj, new string[] { "a", "b" });
            Assert.AreEqual("a", obj.Value1);
            Assert.AreEqual("b", obj.Value2);
        }

        [TestMethod]
        public void ParseCommandLineArguments_NamedArgument() 
        {
            {
                var obj = new ExampleObject();
                ArgumentParser.ParseCommandLineArguments(obj, new string[] { "--Value3", "c" });
                Assert.AreEqual("c", obj.Value3);
            }
            {
                var obj = new ExampleObject();
                ArgumentParser.ParseCommandLineArguments(obj, new string[] { "--Value3", "c", "-Value1", "a" });
                Assert.AreEqual("a", obj.Value1);
                Assert.IsTrue(string.IsNullOrEmpty(obj.Value2));
                Assert.AreEqual("c", obj.Value3);
            }
        }

        public enum EnumDefinition
        {
            One,
            Two,
            Three,
        }
        public class EnumClass
        {
            [Argument]
            public EnumDefinition EnumValue {get;set;}
        }

        [TestMethod]
        public void ParseCommandLineArguments_EnumValue() 
        {
            {
                var obj = new EnumClass();
                ArgumentParser.ParseCommandLineArguments(obj, new string[] { "--EnumValue", "Three" });
                Assert.AreEqual(EnumDefinition.Three, obj.EnumValue);
            }
        }


    }
}