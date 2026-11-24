using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Eutopos.Core.Capture
{
    public enum SessionStage { Recording, Testing, Packaged, Uploading, Submitted }

    public sealed class CaptureSession
    {
        sealed class Manifest
        {
            public string area_name;
            public string area_id;
            public int? building_id;
            public DateTime? started_utc;
            public int tracking_losses;
            public SessionStage stage;
            public int? job_id;
        }

        static readonly string[] Roles = { "peta", "uji" };
        readonly Manifest manifest;
        readonly List<FrameRecord> mapping = new List<FrameRecord>();
        readonly List<FrameRecord> queries = new List<FrameRecord>();

        public string Dir { get; }
        public string AreaName => manifest.area_name;
        public string AreaId => manifest.area_id;
        public int? BuildingId => manifest.building_id;
        public DateTime StartedUtc => manifest.started_utc ?? StartFromDirectory(Dir);
        public int TrackingLosses => manifest.tracking_losses;
        public SessionStage Stage => manifest.stage;
        public int? JobId => manifest.job_id;
        public string RecoveryNote { get; private set; }

        CaptureSession(string dir, Manifest manifest)
        {
            Dir = dir;
            this.manifest = manifest;
        }

        public static CaptureSession Create(string root, string areaName, string areaId, DateTime now)
            => Create(root, areaName, areaId, null, now);

        public static CaptureSession Create(string root, string areaName, string areaId, int? buildingId,
            DateTime now)
        {
            if (!global::Eutopos.Core.Capture.AreaId.IsValid(areaId)) throw new ArgumentException("ID area tidak sah.", nameof(areaId));
            string dir = Path.Combine(root, now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            if (Directory.Exists(dir)) throw new IOException("Folder sesi sudah ada: " + dir);

            var session = new CaptureSession(dir, new Manifest
            {
                area_name = areaName, area_id = areaId, building_id = buildingId,
                started_utc = now.ToUniversalTime(), stage = SessionStage.Recording,
            });
            foreach (string role in Roles)
            {
                Directory.CreateDirectory(session.ImagesPath(role));
                session.WriteRecords(role, session.List(role), 0);
            }
            session.SaveManifest();
            return session;
        }

        public static CaptureSession Open(string sessionDir)
        {
            var session = new CaptureSession(sessionDir, ReadManifest(sessionDir));
            bool recovered = DeleteIfExists(Path.Combine(sessionDir, "session.json.tmp"));
            foreach (string role in Roles)
                recovered |= session.Recover(role);
            if (recovered) session.RecoveryNote = "Sesi dipulihkan. Berkas foto atau catatan yang tidak lengkap dibuang.";
            return session;
        }

        public static IReadOnlyList<string> Unfinished(string root) => ListSessions(root, false);

        public static IReadOnlyList<string> Submitted(string root) => ListSessions(root, true);

        static IReadOnlyList<string> ListSessions(string root, bool submitted)
        {
            var result = new List<string>();
            if (!Directory.Exists(root)) return result;
            foreach (string dir in Directory.EnumerateDirectories(root).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                if (!File.Exists(Path.Combine(dir, "session.json"))) continue;
                try
                {
                    if ((ReadManifest(dir).stage == SessionStage.Submitted) == submitted) result.Add(dir);
                }
                catch (JsonException)
                {
                    // Folder tanpa manifest yang utuh bukan sesi yang bisa dilanjutkan.
                }
            }
            return result;
        }

        public void SetStage(SessionStage s)
        {
            var previous = manifest.stage;
            manifest.stage = s;
            try { SaveManifest(); }
            catch { manifest.stage = previous; throw; }
        }

        public void SetJobId(int id)
        {
            var previous = manifest.job_id;
            manifest.job_id = id;
            try { SaveManifest(); }
            catch { manifest.job_id = previous; throw; }
        }

        public void RecordTrackingLoss()
        {
            manifest.tracking_losses++;
            try { SaveManifest(); }
            catch { manifest.tracking_losses--; throw; }
        }

        public string UploadStatePath(string role)
        {
            List(role);
            return Path.Combine(Dir, "upload-" + role + ".zip.json");
        }

        public void ResetUpload()
        {
            int? previousJob = manifest.job_id;
            SessionStage previousStage = manifest.stage;
            manifest.job_id = null;
            manifest.stage = SessionStage.Packaged;
            try { SaveManifest(); }
            catch { manifest.job_id = previousJob; manifest.stage = previousStage; throw; }
            foreach (string role in Roles) File.Delete(UploadStatePath(role));
        }

        static DateTime StartFromDirectory(string dir) =>
            DateTime.TryParseExact(Path.GetFileName(dir), "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var start) ? start : Directory.GetCreationTimeUtc(dir);

        public int Count(string role) => List(role).Count;
        public IReadOnlyList<FrameRecord> Records(string role) => List(role).AsReadOnly();

        public string Add(string role, byte[] jpeg, FrameRecord record)
        {
            var records = List(role);
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8 ||
                jpeg[2] != 0xff || jpeg[jpeg.Length - 2] != 0xff || jpeg[jpeg.Length - 1] != 0xd9)
                throw new ArgumentException("JPEG tidak utuh.", nameof(jpeg));
            string name = ImageName(records.Count + 1);
            string image = Path.Combine(ImagesPath(role), name);
            string previousName = record.File;
            record.File = name;

            FrameRecord saved;
            try
            {
                string json = FrameRecord.ToLine(record);
                saved = FrameRecord.FromLine(json);
                byte[] line = Encoding.UTF8.GetBytes(json + "\n");
                CaptureDisk.Write(image, stream => stream.Write(jpeg, 0, jpeg.Length));
                try
                {
                    using var frames = new FileStream(FramesPath(role), FileMode.Open, FileAccess.Write);
                    long length = frames.Length;
                    frames.Position = length;
                    try
                    {
                        frames.Write(line, 0, line.Length);
                        frames.Flush(true);
                    }
                    catch
                    {
                        frames.SetLength(length);
                        frames.Flush(true);
                        throw;
                    }
                }
                catch
                {
                    File.Delete(image);
                    throw;
                }
            }
            catch
            {
                record.File = previousName;
                throw;
            }

            records.Add(saved);
            return name;
        }

        public bool UndoLast(string role)
        {
            var records = List(role);
            if (records.Count == 0) return false;
            int last = records.Count - 1;
            var record = records[last];
            WriteRecords(role, records, last);
            records.RemoveAt(last);
            File.Delete(Path.Combine(ImagesPath(role), record.File));
            return true;
        }

        bool Recover(string role)
        {
            var records = List(role);
            string frames = FramesPath(role);
            string images = ImagesPath(role);
            Directory.CreateDirectory(images);
            bool rewrite = !File.Exists(frames);

            if (!rewrite)
            {
                foreach (string line in File.ReadLines(frames))
                {
                    FrameRecord record;
                    try { record = FrameRecord.FromLine(line); }
                    catch (Exception e) when (e is JsonException || e is ArgumentException ||
                        e is FormatException || e is InvalidCastException || e is OverflowException)
                    {
                        rewrite = true;
                        continue;
                    }

                    // Nama yang diharapkan juga mencegah akses keluar dari folder images.
                    if (record.File != ImageName(records.Count + 1) ||
                        !CompleteJpeg(Path.Combine(images, record.File)))
                    {
                        rewrite = true;
                        break;
                    }
                    records.Add(record);
                }

                // Penulisan JSON bisa selesai tepat sebelum proses mati saat menulis pemisah baris.
                using var input = File.OpenRead(frames);
                if (input.Length > 0)
                {
                    input.Seek(-1, SeekOrigin.End);
                    rewrite |= input.ReadByte() != '\n';
                }
            }

            bool recovered = DeleteIfExists(frames + ".tmp");
            if (rewrite) WriteRecords(role, records, records.Count);
            var kept = new HashSet<string>(records.Select(r => r.File), StringComparer.Ordinal);
            foreach (string image in Directory.EnumerateFiles(images))
            {
                string name = Path.GetFileName(image);
                if ((name.EndsWith(".jpg", StringComparison.Ordinal) ||
                    name.EndsWith(".tmp", StringComparison.Ordinal)) && !kept.Contains(name))
                    recovered |= DeleteIfExists(image);
            }

            return rewrite || recovered;
        }

        static bool CompleteJpeg(string path)
        {
            if (!File.Exists(path)) return false;
            using var input = File.OpenRead(path);
            if (input.Length < 4 || input.ReadByte() != 0xff || input.ReadByte() != 0xd8 ||
                input.ReadByte() != 0xff)
                return false;
            input.Seek(-2, SeekOrigin.End);
            return input.ReadByte() == 0xff && input.ReadByte() == 0xd9;
        }

        static string ImageName(int number) => number.ToString("D6", CultureInfo.InvariantCulture) + ".jpg";

        static bool DeleteIfExists(string path)
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        List<FrameRecord> List(string role) => role switch
        {
            "peta" => mapping,
            "uji" => queries,
            _ => throw new ArgumentException("Peran foto tidak sah.", nameof(role)),
        };

        string ImagesPath(string role) => Path.Combine(Dir, role, "images");
        string FramesPath(string role) => Path.Combine(Dir, role, "frames.jsonl");

        static Manifest ReadManifest(string dir) =>
            JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(Path.Combine(dir, "session.json"))) ??
            throw new JsonSerializationException("Manifest sesi kosong.");

        void SaveManifest() => CaptureDisk.WriteText(Path.Combine(Dir, "session.json"),
            JsonConvert.SerializeObject(manifest));

        void WriteRecords(string role, List<FrameRecord> records, int count)
        {
            CaptureDisk.Write(FramesPath(role), stream =>
            {
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
                writer.NewLine = "\n";
                for (int i = 0; i < count; i++)
                    writer.WriteLine(FrameRecord.ToLine(records[i]));
            });
        }
    }

    // Berkas sementara berada pada volume tujuan agar penggantian dilakukan dengan rename.
    static class CaptureDisk
    {
        public static void WriteText(string path, string text)
        {
            Write(path, stream =>
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                stream.Write(data, 0, data.Length);
            });
        }

        public static void Write(string path, Action<FileStream> write)
        {
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
