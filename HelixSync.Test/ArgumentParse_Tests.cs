using Xunit;

namespace HelixSync.Test
{
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

        [Fact]
        public void ParseCommandLineArguments_OrdinalPositions()
        {
            var obj = new ExampleObject();
            ArgumentParser.ParseCommandLineArguments(obj, new string[] { "a", "b" });
            Assert.Equal("a", obj.Value1);
            Assert.Equal("b", obj.Value2);
        }

        [Fact]
        public void ParseCommandLineArguments_NamedArgument() 
        {
            {
                var obj = new ExampleObject();
                ArgumentParser.ParseCommandLineArguments(obj, new string[] { "--Value3", "c" });
                Assert.Equal("c", obj.Value3);
            }
            {
                var obj = new ExampleObject();
                ArgumentParser.ParseCommandLineArguments(obj, new string[] { "--Value3", "c", "-Value1", "a" });
                Assert.Equal("a", obj.Value1);
                Assert.True(string.IsNullOrEmpty(obj.Value2));
                Assert.Equal("c", obj.Value3);
            }
        }
    }
}