using System;
using System.Collections;
using System.Collections.Generic;
using Eutopos.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Eutopos.App
{
    public sealed class VpsClient : MonoBehaviour
    {
        public VpsConfig Config;
        public int TimeoutS = 15;

        public IEnumerator Localize(byte[] jpeg, Intrinsics k, Action<LocalizeResult, float> done)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("image", jpeg, "frame.jpg", "image/jpeg"),
                new MultipartFormDataSection("fx", VpsResponses.Number(k.Fx)),
                new MultipartFormDataSection("fy", VpsResponses.Number(k.Fy)),
                new MultipartFormDataSection("cx", VpsResponses.Number(k.Cx)),
                new MultipartFormDataSection("cy", VpsResponses.Number(k.Cy)),
            };
            using (var req = UnityWebRequest.Post(Config.BaseUrl + "/localize", form))
            {
                Prepare(req);
                float t0 = Time.realtimeSinceStartup;
                yield return req.SendWebRequest();
                float latency = Time.realtimeSinceStartup - t0;
                bool connection = req.result == UnityWebRequest.Result.ConnectionError;
                done(VpsResponses.ClassifyLocalize(req.responseCode, req.downloadHandler?.text, connection), latency);
            }
        }

        public IEnumerator FetchNavGraph(Action<NavGraphResult> done)
        {
            using (var req = UnityWebRequest.Get(Config.BaseUrl + "/navgraph"))
            {
                Prepare(req);
                yield return req.SendWebRequest();
                bool connection = req.result == UnityWebRequest.Result.ConnectionError;
                done(VpsResponses.ParseNavGraph(req.responseCode, req.downloadHandler?.text, connection));
            }
        }

        void Prepare(UnityWebRequest req)
        {
            req.timeout = TimeoutS;
            req.redirectLimit = 0; // Access mengalihkan ke halaman login: tampilkan sebagai 3xx, jangan diikuti
            req.SetRequestHeader("CF-Access-Client-Id", Config.ClientId);
            req.SetRequestHeader("CF-Access-Client-Secret", Config.ClientSecret);
        }
    }
}
