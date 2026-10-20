using System.IO;
using System.Text;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class LogFilesTests
    {
        [Test]
        public void ExportNameCarriesTheAreaAndSessionStamp()
        {
            Assert.That(LogFiles.ExportName("lab10", "/data/files/logs/20261017-091500.jsonl"),
                Is.EqualTo("eutopos-lab10-20261017-091500.jsonl"));
            Assert.That(LogFiles.ExportName(null, "/data/files/logs/20261017-091500.jsonl"),
                Is.EqualTo("eutopos-20261017-091500.jsonl"));
        }

        [Test]
        public void AreaIdCannotEscapeTheFileName()
        {
            Assert.That(LogFiles.ExportName("a/b\\c", "x/20261017-091500.jsonl"),
                Is.EqualTo("eutopos-a_b_c-20261017-091500.jsonl"));
        }

        [Test]
        public void LogStillOpenForWritingCanBeReadForExport()
        {
            // SessionLog menahan berkas terbuka sepanjang sesi; ekspor terjadi di tengah sesi.
            var path = Path.Combine(Path.GetTempPath(), "eutopos-logfiles-test.jsonl");
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true })
            {
                w.WriteLine("{\"type\":\"session_start\"}");
                Assert.That(Encoding.UTF8.GetString(LogFiles.ReadShared(path)), Does.Contain("session_start"));
            }
            File.Delete(path);
        }
    }
}
