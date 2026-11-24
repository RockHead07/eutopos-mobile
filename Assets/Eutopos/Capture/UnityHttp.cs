using System.Collections.Generic;
using System.Threading.Tasks;
using Eutopos.Core.Capture;
using UnityEngine;
using UnityEngine.Networking;

namespace Eutopos.Capture
{
    public sealed class UnityHttp : ICaptureHttp
    {
        readonly HashSet<UnityWebRequest> uploads = new HashSet<UnityWebRequest>();

        // Permintaan job non-idempoten diselesaikan supaya ID respons dapat dipersistenkan.
        public void AbortUploads()
        {
            foreach (var request in new List<UnityWebRequest>(uploads)) request.Abort();
        }

        public async Task<HttpReply> Send(HttpCall call)
        {
            using var request = new UnityWebRequest(call.Url, call.Method);
            request.redirectLimit = 0;
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (call.Body != null) request.uploadHandler = new UploadHandlerRaw(call.Body);
            foreach (var header in call.Headers) request.SetRequestHeader(header.Key, header.Value);
            bool upload = call.Headers.ContainsKey("Tus-Resumable");
            if (upload) uploads.Add(request);
            try { await Awaitable.FromAsyncOperation(request.SendWebRequest()); }
            finally { if (upload) uploads.Remove(request); }

            return new HttpReply
            {
                Status = request.responseCode,
                Headers = request.GetResponseHeaders() ?? new Dictionary<string, string>(),
                Text = request.downloadHandler.text,
                // Beberapa respons HTTP, termasuk pengalihan, juga dapat diberi status ConnectionError.
                NetworkError = request.responseCode == 0 &&
                    (request.result == UnityWebRequest.Result.ConnectionError ||
                     request.result == UnityWebRequest.Result.DataProcessingError),
            };
        }
    }
}
