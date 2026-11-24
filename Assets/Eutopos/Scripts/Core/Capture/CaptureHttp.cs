using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core.Capture
{
    public sealed class HttpCall
    {
        public string Method;
        public string Url;
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Body;
    }

    public sealed class HttpReply
    {
        public long Status;
        public Dictionary<string, string> Headers;
        public string Text;
        public bool NetworkError;

        internal string Header(string name)
        {
            if (Headers != null)
                foreach (var header in Headers)
                    if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                        return header.Value;
            return null;
        }
    }

    public interface ICaptureHttp
    {
        Task<HttpReply> Send(HttpCall call);
    }

    public sealed class CaptureAuth
    {
        public string BaseUrl;
        public string ClientId;
        public string ClientSecret;
        public string AppToken;

        public void Apply(HttpCall c)
        {
            c.Headers["CF-Access-Client-Id"] = ClientId;
            c.Headers["CF-Access-Client-Secret"] = ClientSecret;
            c.Headers["Authorization"] = "Bearer " + AppToken;
        }
    }

    public enum FailureKind { None, Retryable, TokenRejected, AccessRedirect, JobClosed, UploadGone, Fatal }

    public sealed class CaptureHttpException : Exception
    {
        public FailureKind Kind { get; }
        public long Status { get; }
        public string Detail { get; }

        public CaptureHttpException(FailureKind kind, long status, string detail) : base(detail)
        {
            Kind = kind;
            Status = status;
            Detail = detail;
        }
    }

    public static class CaptureHttp
    {
        public const int MaxAttempts = 8;

        public static FailureKind Classify(HttpReply r)
        {
            if (r.NetworkError || r.Status == 408 || r.Status == 429 || (r.Status >= 500 && r.Status <= 599))
                return FailureKind.Retryable;
            if (r.Status == 401 || r.Status == 403) return FailureKind.TokenRejected;
            if (r.Status >= 300 && r.Status <= 399) return FailureKind.AccessRedirect;
            if (r.Status == 409) return FailureKind.JobClosed;
            if (r.Status == 404 || r.Status == 410) return FailureKind.UploadGone;
            if (r.Status >= 200 && r.Status <= 299) return FailureKind.None;
            return FailureKind.Fatal;
        }

        public static TimeSpan RetryDelay(int attempt) =>
            TimeSpan.FromSeconds(attempt >= 5 ? 30 : 1 << Math.Max(0, attempt));

        internal static CaptureHttpException Error(HttpReply reply, FailureKind kind)
        {
            string detail = null;
            if (!string.IsNullOrEmpty(reply.Text))
            {
                try
                {
                    var value = JObject.Parse(reply.Text)["detail"];
                    if (value != null && value.Type != JTokenType.Null)
                        detail = value.Type == JTokenType.String ? (string)value : value.ToString(Formatting.None);
                }
                catch (JsonException)
                {
                    // Halaman galat proxy bukan detail yang dapat ditampilkan.
                }
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = kind switch
                {
                    FailureKind.TokenRejected => "Akses ditolak. Periksa token aplikasi dan konfigurasi Access.",
                    FailureKind.AccessRedirect => "Access mengalihkan permintaan. Periksa konfigurasi akses server.",
                    FailureKind.JobClosed => "Pekerjaan tidak menerima unggahan lagi. Buat pekerjaan baru dari paket ini.",
                    FailureKind.UploadGone => "Unggahan sudah tidak tersedia.",
                    FailureKind.Retryable => "Server belum dapat dijangkau. Coba lanjutkan unggahan.",
                    _ => $"Permintaan ditolak (HTTP {reply.Status}).",
                };
            }
            return new CaptureHttpException(kind, reply.Status, detail);
        }
    }

    public sealed class BuildingInfo
    {
        public int Id;
        public string Slug;
        public string Name;
        public IReadOnlyList<string> AreaIds = Array.Empty<string>();
    }

    public sealed class CaptureApi
    {
        readonly ICaptureHttp http;
        readonly CaptureAuth auth;

        public CaptureApi(ICaptureHttp http, CaptureAuth auth)
        {
            this.http = http;
            this.auth = auth;
        }

        public async Task<int> CreateJob(string areaId, string areaName,
            IReadOnlyList<(string name, string role, long size)> files, int? buildingId = null)
        {
            var payload = new JObject
            {
                ["area_id"] = areaId,
                ["area_name"] = areaName,
                ["videos"] = new JArray(files.Select(file => new JObject
                {
                    ["name"] = file.name, ["role"] = file.role, ["size"] = file.size, ["kind"] = "frames",
                })),
            };
            if (buildingId.HasValue) payload["building_id"] = buildingId.Value;
            var call = new HttpCall
            {
                Method = "POST", Url = auth.BaseUrl.TrimEnd('/') + "/api/jobs",
                Body = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)),
            };
            call.Headers["Content-Type"] = "application/json";
            return await SendJob(call, json => (int)json["id"]);
        }

        public Task<JobSnapshot> GetJob(int jobId)
        {
            var call = new HttpCall
            {
                Method = "GET", Url = auth.BaseUrl.TrimEnd('/') + "/api/jobs/" + jobId.ToString(CultureInfo.InvariantCulture),
            };
            return SendJob(call, json => JobSnapshot.FromJson((JObject)json));
        }

        public Task<IReadOnlyList<BuildingInfo>> ListBuildings()
        {
            var call = new HttpCall { Method = "GET", Url = auth.BaseUrl.TrimEnd('/') + "/api/buildings" };
            return SendJob<IReadOnlyList<BuildingInfo>>(call, json => ((JArray)json).Select(row =>
                new BuildingInfo
                {
                    Id = (int)row["id"], Slug = (string)row["slug"], Name = (string)row["name"],
                    AreaIds = ((JArray)row["area_ids"]).Values<string>().ToArray(),
                }).ToArray());
        }

        async Task<T> SendJob<T>(HttpCall call, Func<JToken, T> parse)
        {
            auth.Apply(call);
            var reply = await http.Send(call);
            var kind = CaptureHttp.Classify(reply);
            if (kind != FailureKind.None) throw CaptureHttp.Error(reply, kind);
            try { return parse(JToken.Parse(reply.Text)); }
            catch (Exception e) when (e is JsonException || e is ArgumentException ||
                e is InvalidCastException || e is InvalidOperationException || e is FormatException ||
                e is OverflowException)
            {
                throw new CaptureHttpException(FailureKind.Fatal, reply.Status, "Jawaban API tidak sah.");
            }
        }
    }

    public sealed class UploadState
    {
        public int JobId;
        public string Name;
        public string Path;
        public long Size;
        public string Url;
        public long Offset;

        public void Save(string stateFile) => CaptureDisk.WriteText(stateFile, JsonConvert.SerializeObject(this));

        public static UploadState Load(string stateFile) =>
            JsonConvert.DeserializeObject<UploadState>(File.ReadAllText(stateFile)) ??
            throw new JsonSerializationException("Keadaan unggah kosong.");

        public static UploadState LoadOrCreate(string stateFile, int jobId, string name, string path, long size)
        {
            if (File.Exists(stateFile))
            {
                try
                {
                    var state = Load(stateFile);
                    if (state.JobId == jobId && state.Name == name && state.Size == size)
                    {
                        state.Path = path;
                        return state;
                    }
                }
                catch (JsonException)
                {
                    // Keadaan yang rusak tidak dapat dipakai untuk melanjutkan unggahan.
                }
            }
            return new UploadState { JobId = jobId, Name = name, Path = path, Size = size };
        }
    }

    public sealed class TusUploader
    {
        readonly ICaptureHttp http;
        readonly CaptureAuth auth;
        readonly int chunkBytes;
        readonly Func<TimeSpan, Task> delay;

        public TusUploader(ICaptureHttp http, CaptureAuth auth, int chunkBytes = 50 * 1024 * 1024,
            Func<TimeSpan, Task> delay = null)
        {
            if (chunkBytes <= 0) throw new ArgumentOutOfRangeException(nameof(chunkBytes));
            this.http = http;
            this.auth = auth;
            this.chunkBytes = chunkBytes;
            this.delay = delay;
        }

        public async Task Upload(UploadState st, string stateFile, IProgress<double> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (st.Url != null && st.Size > 0 && st.Offset == st.Size)
            {
                // Server sudah mengonfirmasi ukuran penuh. Pekerja dapat sudah menghapus berkas tus.
                progress?.Report(1);
                return;
            }

            var endpoint = new Uri(auth.BaseUrl.TrimEnd('/') + "/files/");
            using var file = File.OpenRead(st.Path);
            if (file.Length != st.Size || st.Size <= 0)
                throw new IOException("Ukuran paket berbeda dari ukuran yang didaftarkan.");

            if (st.Url != null)
            {
                var url = UploadUrl(endpoint, st.Url, 0);
                var (head, _) = await Send(Request("HEAD", url), ct);
                if (CaptureHttp.Classify(head) == FailureKind.UploadGone)
                {
                    st.Url = null;
                    st.Offset = 0;
                    st.Save(stateFile);
                }
                else
                {
                    EnsureSuccess(head);
                    st.Offset = ReadOffset(head, 0, st.Size);
                    st.Save(stateFile);
                }
            }

            if (st.Url == null)
            {
                var create = Request("POST", endpoint);
                create.Headers["Upload-Length"] = Number(st.Size);
                create.Headers["Upload-Metadata"] = $"job_id {Base64(Number(st.JobId))},name {Base64(st.Name)}";
                var (reply, _) = await Send(create, ct);
                EnsureSuccess(reply);
                st.Url = UploadUrl(endpoint, reply.Header("Location"), reply.Status).AbsoluteUri;
                st.Offset = 0;
                st.Save(stateFile);
            }

            var uploadUrl = UploadUrl(endpoint, st.Url, 0);
            progress?.Report((double)st.Offset / st.Size);
            while (st.Offset < st.Size)
            {
                ct.ThrowIfCancellationRequested();
                int count = (int)Math.Min(chunkBytes, st.Size - st.Offset);
                var data = new byte[count];
                file.Position = st.Offset;
                for (int read = 0; read < count;)
                {
                    int n = file.Read(data, read, count - read);
                    if (n == 0) throw new EndOfStreamException("Paket terpotong selama unggahan.");
                    read += n;
                }

                var patch = Request("PATCH", uploadUrl);
                patch.Headers["Content-Type"] = "application/offset+octet-stream";
                patch.Headers["Upload-Offset"] = Number(st.Offset);
                patch.Body = data;
                var (reply, retried) = await Send(patch, ct);
                if (retried && CaptureHttp.Classify(reply) == FailureKind.JobClosed)
                {
                    // Respons sebelumnya dapat hilang walaupun server sudah menulis potongan.
                    var (head, _) = await Send(Request("HEAD", uploadUrl), ct);
                    if (CaptureHttp.Classify(head) == FailureKind.None && ReadOffset(head, 0, st.Size) > st.Offset)
                        reply = head;
                }
                EnsureSuccess(reply);
                st.Offset = ReadOffset(reply, st.Offset + 1, st.Offset + count);
                st.Save(stateFile);
                progress?.Report((double)st.Offset / st.Size);
            }
        }

        HttpCall Request(string method, Uri uri)
        {
            var call = new HttpCall { Method = method, Url = uri.AbsoluteUri };
            call.Headers["Tus-Resumable"] = "1.0.0";
            auth.Apply(call);
            return call;
        }

        async Task<(HttpReply reply, bool retried)> Send(HttpCall call, CancellationToken ct)
        {
            for (int attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var reply = await http.Send(call);
                if (CaptureHttp.Classify(reply) != FailureKind.Retryable || attempt + 1 == CaptureHttp.MaxAttempts)
                    return (reply, attempt > 0);
                TimeSpan duration = CaptureHttp.RetryDelay(attempt);
                if (delay == null) await Task.Delay(duration, ct);
                else await delay(duration);
            }
        }

        static void EnsureSuccess(HttpReply reply)
        {
            var kind = CaptureHttp.Classify(reply);
            if (kind != FailureKind.None) throw CaptureHttp.Error(reply, kind);
        }

        static long ReadOffset(HttpReply reply, long min, long max)
        {
            if (!long.TryParse(reply.Header("Upload-Offset"), NumberStyles.None,
                CultureInfo.InvariantCulture, out long value) || value < min || value > max)
                throw new CaptureHttpException(FailureKind.Fatal, reply.Status, "Offset unggah dari server tidak sah.");
            return value;
        }

        static Uri UploadUrl(Uri endpoint, string location, long status)
        {
            // Header autentikasi hanya boleh dikirim kembali ke origin yang dikonfigurasi.
            if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(endpoint, location, out Uri uri) ||
                uri.Scheme != endpoint.Scheme || uri.IdnHost != endpoint.IdnHost || uri.Port != endpoint.Port ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new CaptureHttpException(FailureKind.Fatal, status, "URL unggah dari server tidak sah.");
            return uri;
        }

        static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
