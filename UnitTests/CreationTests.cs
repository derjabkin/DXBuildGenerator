using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DXBuildGenerator;
using CommandLine;
using System.IO;
using System.Text;
using DXBuildGenerator.Properties;
namespace UnitTests {
    [TestClass]
    public class CreationTests {

        [TestMethod]
        public void TestEmptyParameters() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, new string[0]);
            Assert.IsNull(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.IsTrue(output.EndsWith(Resources.UseHelpOptionForUsage));
            Assert.IsTrue(output.Contains(Resources.NoPathSpecifiedMessage));

        }


        [TestMethod]
        public void TestEmptySourceDir() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, new string[] { "-r", "Valid" });
            Assert.IsNull(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.IsTrue(output.Contains(Resources.NoPathSpecifiedMessage));

        }

        [TestMethod]
        public void TestEmptyReferenceDir() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, new string[] { "-s", "Valid" });
            Assert.IsNull(generator);
            string output = sb.ToString().Trim('\r', '\n');
            Assert.IsTrue(output.Contains(Resources.NoPathSpecifiedMessage));

        }

        [TestMethod]
        public void TestValidRootPath() {
            StringBuilder sb = new StringBuilder();

            var parser = CreateTestParser(sb);
            var generator = BuildGenerator.Create(parser, new string[] { "-x", "Valid" });
            string output = sb.ToString().Trim('\r', '\n');
            Assert.AreEqual("Valid\\Bin\\Framework", generator.ReferencesPath);
            Assert.AreEqual("Valid\\Bin\\Framework", generator.OutputPath);
            Assert.AreEqual("Valid\\Sources", generator.SourceCodeDir);
            Assert.IsNotNull(generator);
        }

        private Parser CreateTestParser(StringBuilder sb) {
            ParserSettings settings = new ParserSettings(new StringWriter(sb));
            return new Parser(settings);
        }
    }
}
