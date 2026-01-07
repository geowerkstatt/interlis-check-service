using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Geowerkstatt.Ilicop.Web.XtfLog
{
    [TestClass]
    public class XtfLogParserTest
    {
        [TestMethod]
        [DeploymentItem(@"testdata/xtflog/valid.xtf", "xtflog")]
        public void ParseValidXml()
        {
            using var reader = new StreamReader(File.OpenRead(Path.Combine("xtflog", "valid.xtf")));

            var logEntries = XtfLogParser.Parse(reader);

            Assert.HasCount(2, logEntries);

            var first = logEntries[0];
            Assert.AreEqual("1", first.Tid);
            Assert.AreEqual("Error message 1", first.Message);
            Assert.AreEqual("Error", first.Type);
            Assert.AreEqual("Model.Topic.Class", first.ObjTag);
            Assert.AreEqual("Data source 1", first.DataSource);
            Assert.AreEqual(11, first.Line);
            Assert.AreEqual("Technical details 1", first.TechDetails);
            Assert.AreEqual(123.456m, first.Geometry.Coord.C1);
            Assert.AreEqual(234.567m, first.Geometry.Coord.C2);

            var second = logEntries[1];
            Assert.AreEqual("2", second.Tid);
            Assert.AreEqual("Info message 2", second.Message);
            Assert.AreEqual("Info", second.Type);
            Assert.IsNull(second.ObjTag);
            Assert.IsNull(second.DataSource);
            Assert.IsNull(second.Line);
            Assert.IsNull(second.TechDetails);
            Assert.IsNull(second.Geometry);
        }

        [TestMethod]
        [DeploymentItem(@"testdata/xtflog/invalid.xtf", "xtflog")]
        public void ParseInvalidXml()
        {
            using var reader = new StreamReader(File.OpenRead(Path.Combine("xtflog", "invalid.xtf")));

            Assert.ThrowsExactly<InvalidOperationException>(() => XtfLogParser.Parse(reader));
        }

        [TestMethod]
        [DeploymentItem(@"testdata/xtflog/empty.xtf", "xtflog")]
        public void ParseEmptyXml()
        {
            using var reader = new StreamReader(File.OpenRead(Path.Combine("xtflog", "empty.xtf")));

            var logEntries = XtfLogParser.Parse(reader);

            Assert.IsEmpty(logEntries);
        }
    }
}
