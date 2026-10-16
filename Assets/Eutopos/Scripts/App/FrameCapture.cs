using System;
using Eutopos.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Eutopos.App
{
    public sealed class CapturedFrame
    {
        public byte[] Jpeg;
        public Intrinsics K;           // sudah diputar tegak dan diskalakan ke ukuran JPEG
        public UnityEngine.Vector3 CamPosU;
        public UnityEngine.Vector3 CamFwdU;
        public long TimestampNs;
        public float CapturedAtS;      // Time.realtimeSinceStartup saat frame diambil
    }

    /// <summary>Gambar CPU ARCore + pose kamera pada frame yang sama, diputar tegak (spesifikasi bagian 6).</summary>
    public sealed class FrameCapture : MonoBehaviour
    {
        public ARCameraManager CameraManager;
        public Camera ArCamera;
        public int MaxLongSide = 1920;

        Action<CapturedFrame, string> pending;
        bool configured;

        /// <summary>Dipanggil sekali setelah AddComponent (OnEnable berjalan sebelum field bisa diisi).</summary>
        public void Init(ARCameraManager cameraManager, Camera arCamera)
        {
            CameraManager = cameraManager;
            ArCamera = arCamera;
            CameraManager.frameReceived += OnFrame;
        }

        void OnDestroy()
        {
            if (CameraManager != null) CameraManager.frameReceived -= OnFrame;
        }

        public void Request(Action<CapturedFrame, string> done) => pending = done;

        /// <summary>Pilih konfigurasi kamera dengan resolusi gambar CPU terbesar yang tidak melebihi MaxLongSide.</summary>
        void SelectCameraConfiguration()
        {
            using (var configs = CameraManager.GetConfigurations(Allocator.Temp))
            {
                XRCameraConfiguration? best = null;
                foreach (var c in configs)
                {
                    int longSide = Mathf.Max(c.width, c.height);
                    if (longSide > MaxLongSide) continue;
                    if (best == null || longSide > Mathf.Max(best.Value.width, best.Value.height)) best = c;
                }
                if (best != null) CameraManager.currentConfiguration = best;
            }
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            if (!configured) { configured = true; SelectCameraConfiguration(); }
            if (pending == null) return;
            var done = pending;
            pending = null;

            CapturedFrame frame;
            string error;
            // done dipanggil sekali, di luar try: galat konversi tidak boleh membuat koreksi macet (scheduler.Finish)
            try { error = Capture(args, out frame); }
            catch (Exception e) { frame = null; error = "gambar kamera gagal diproses: " + e.Message; }
            done(frame, error);
        }

        string Capture(ARCameraFrameEventArgs args, out CapturedFrame frame)
        {
            frame = null;
            var camPos = ArCamera.transform.position;
            var camFwd = ArCamera.transform.forward;
            if (!CameraManager.TryGetIntrinsics(out var intr)) return "intrinsik kamera tidak tersedia";
            if (!CameraManager.TryAcquireLatestCpuImage(out var image)) return "gambar kamera tidak tersedia";
            try
            {
                int w = image.width, h = image.height;
                var p = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, XRCpuImage.Transformation.None);
                var buffer = new NativeArray<byte>(image.GetConvertedDataSize(p), Allocator.Temp);
                try
                {
                    image.Convert(p, buffer);
                    var upright = ImageOps.RotateClockwiseBottomUp(buffer.ToArray(), w, h);
                    var tex = new Texture2D(h, w, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(upright);
                    tex.Apply();
                    var jpeg = tex.EncodeToJPG(90);
                    Destroy(tex);
                    var k = new Intrinsics(intr.focalLength.x, intr.focalLength.y, intr.principalPoint.x, intr.principalPoint.y,
                                           intr.resolution.x, intr.resolution.y)
                        .ScaledTo(w, h)
                        .RotatedClockwise();
                    frame = new CapturedFrame
                    {
                        Jpeg = jpeg, K = k, CamPosU = camPos, CamFwdU = camFwd,
                        TimestampNs = args.timestampNs ?? 0, CapturedAtS = Time.realtimeSinceStartup,
                    };
                    return null;
                }
                finally { buffer.Dispose(); }
            }
            finally { image.Dispose(); }
        }
    }
}
