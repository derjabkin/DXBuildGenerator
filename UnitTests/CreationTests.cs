using CommandLine;
using DXBuildGenerator;
using DXBuildGenerator.Properties;
using System.Text;
using Xunit;
namespace UnitTests
{
    public class CreationTests {

        [Fact]
        public void TestEmptyParameters() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, new string[0]);
            Assert.Null(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.EndsWith(Resources.UseHelpOptionForUsage, output);
            Assert.Contains(Resources.NoPathSpecifiedMessage, output);

        }


        [Fact]
        public void TestEmptySourceDir() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, ["-r", "Valid"]);
            Assert.Null(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.Contains(Resources.NoPathSpecifiedMessage, output);

        }

        [Fact]
        public void TestEmptyReferenceDir() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, ["-s", "Valid"]);
            Assert.Null(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.Contains(Resources.NoPathSpecifiedMessage, output);

        }

        [Fact]
        public void TestValidRootPath() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, ["-x", "Valid"]);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.Equal("Valid\\Bin\\Framework", generator.Options.ReferencesPath);
            Assert.Equal("Valid\\Bin\\Framework", generator.Options.OutputPath);
            Assert.Equal("Valid\\Sources", generator.Options.SourceCodeDir);
            Assert.NotNull(generator);
        }

        private Parser CreateTestParser(StringBuilder sb) {
            return new Parser();
        }
    }
}
