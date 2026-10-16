# eutopos-mobile MVP navigasi satu lantai: Rencana Implementasi

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aplikasi Android (Unity + ARCore) yang melokalisasi pengguna lewat layanan VPS eutopos, menampilkan posisi dan arah hadap, menggambar rute ke target pada graf titik terukur, dan menyatakan Sampai di lantai 10.

**Architecture:** Logika inti (konversi kerangka, penyelarasan 4-DoF, penjaga lompatan, penjadwal koreksi, graf + A*, pembacaan jawaban server) berupa kelas C# murni di assembly `Eutopos.Core` (`noEngineReferences`) yang diuji di EditMode. Lapisan `Eutopos.App` (MonoBehaviour) mengambil frame ARCore, memanggil server, menggeser `BuildingRoot` yang ditempel `ARAnchor`, menggambar rute dan denah mini, serta menulis log JSONL. UI dibangun dari kode, sehingga scene hanya berisi AR Session, XR Origin, dan satu objek `App`.

**Tech Stack:**

| Lapisan | Pilihan | Versi |
|---|---|---|
| Mesin | Unity, Built-in Render Pipeline | 6000.3.14f1 (`D:\Dev\Tools\Unity Editors\6000.3.14f1\Editor\Unity.exe`) |
| AR | AR Foundation, Google ARCore XR Plugin | 6.3.4, 6.3.4 (menarik XR Core Utils 2.5.3, XR Plugin Management 4.5.4) |
| Masukan dan UI | Input System, uGUI (Text bawaan `LegacyRuntime.ttf`) | 1.19.0, 2.0.0 |
| JSON | Newtonsoft Json for Unity | 3.2.2 |
| Tes | Unity Test Framework (NUnit), EditMode | 1.6.0 |
| Bahasa | C# (.NET Standard 2.1), `System.Numerics` di inti | bawaan Unity |
| Target | Android, IL2CPP, ARM64, OpenGL ES 3, min API 24, layar tegak | |
| Server | eutopos-vps: FastAPI, `POST /localize`, `GET /navgraph` | rencana `eutopos-vps/docs/plans/2026-10-15-navgraph-dan-align.md` |
| Jaringan | `UnityWebRequest` + Cloudflare Access service token (header `CF-Access-Client-Id`, `CF-Access-Client-Secret`) | |

Semua versi paket diambil dari `packages-lock.json` proyek DARSI, yang sudah terbukti berjalan bersama di Unity yang sama.

**Spec:** `docs/specs/2026-10-15-mvp-navigasi-design.md` (repo ini).

## Global Constraints

- Unity 6000.3.14f1; paket persis: `com.unity.xr.arfoundation` 6.3.4, `com.unity.xr.arcore` 6.3.4, `com.unity.inputsystem` 1.19.0, `com.unity.ugui` 2.0.0, `com.unity.test-framework` 1.6.0, `com.unity.nuget.newtonsoft-json` 3.2.2.
- Tanpa MultiSet, Photon, AI Navigation, dan avatar. Tidak ada berkas DARSI yang disalin.
- Android: IL2CPP, ARM64, OpenGL ES 3, min API 24, `com.rockhead07.eutopos`, tegak saja, Active Input Handling = Input System.
- Assembly `Eutopos.Core` memakai `noEngineReferences: true`; semua logika yang diuji ada di sana.
- Kerangka: G (server, tangan kanan, x lorong, y kiri saat menghadap +x, z atas, lantai z = 0), B (Unity, `(x, y, z)_G → (x, z, y)`), U (sesi ARCore). Yaw B = `atan2(f.x, f.z)` dalam derajat.
- Ambang: inlier >= 50, lompatan 3 m, koreksi baru-baru ini 30 detik, konfirmasi 1 m, Sampai < 1,0 m, interval koreksi awal 5 detik, batas waktu permintaan 15 detik, kamera menunduk bila komponen mendatar < 0,3.
- Angka yang dikirim ke server selalu `CultureInfo.InvariantCulture` (titik desimal).
- Rahasia di `Assets/Eutopos/Resources/vps-token.local.json` (gitignored). Koordinat gedung dan log lapangan tidak pernah di-commit.
- Lisensi AGPL-3.0. Commit dan push hanya atas perintah eksplisit Bagus; langkah "Commit" di rencana ini adalah titik commit yang diusulkan. Tanpa atribusi AI.
- Teks untuk manusia (UI, log, dokumen) tanpa em dash.

## Review Focus

1. Ponsel berlokal Indonesia memformat `1234.5` sebagai `1234,5`: `fx`, `fy`, `cx`, `cy` harus selalu bertitik desimal, kalau tidak server menolak atau salah membaca. Uji di Task 6.
2. Cloudflare Access dengan token tidak ada atau salah menjawab 302 ke halaman login, bukan 401: aplikasi harus memperlakukan 3xx sebagai "tidak berwenang", bukan mem-parse halaman HTML. Uji di Task 6.
3. Yaw melintasi ±180°: θ dari yaw U = 170° dan yaw B = −170° harus −20°, bukan 340°. Uji di Task 3.
4. Target tidak terjangkau dari sisi terdekat pengguna (graf terpisah): rute harus `null` dan UI memberi pesan, bukan galat. Uji di Task 5.
5. Timer koreksi berbunyi saat permintaan sebelumnya masih berjalan atau pelacakan hilang: tidak boleh ada permintaan kedua. Uji di Task 4.

---

## Fase A: proyek dan logika inti (tanpa ponsel)

### Task 1: Kerangka proyek Unity

**Files:**
- Create: `Assets/`, `Packages/`, `ProjectSettings/` (dibuat Unity)
- Modify: `Packages/manifest.json`
- Create: `.gitignore`, `LICENSE`, `README.md` (ganti isi, UTF-8)
- Create: `Assets/Eutopos/Scripts/Core/Eutopos.Core.asmdef`
- Create: `Assets/Eutopos/Tests/EditMode/Eutopos.Core.Tests.asmdef`
- Create: `Assets/Eutopos/Tests/EditMode/SmokeTests.cs`

**Interfaces:**
- Produces: assembly `Eutopos.Core` (tanpa referensi mesin, referensi `Newtonsoft.Json.dll`) dan `Eutopos.Core.Tests`; perintah uji batchmode yang dipakai semua task berikutnya.

- [ ] **Step 1: Buat proyek Unity di folder sementara lalu pindahkan**

```powershell
& "D:\Dev\Tools\Unity Editors\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -createProject "D:\Dev\Projects\_eutopos-mobile-tmp" -logFile -
Move-Item "D:\Dev\Projects\_eutopos-mobile-tmp\Assets" "D:\Dev\Projects\eutopos-mobile\Assets"
Move-Item "D:\Dev\Projects\_eutopos-mobile-tmp\Packages" "D:\Dev\Projects\eutopos-mobile\Packages"
Move-Item "D:\Dev\Projects\_eutopos-mobile-tmp\ProjectSettings" "D:\Dev\Projects\eutopos-mobile\ProjectSettings"
Remove-Item "D:\Dev\Projects\_eutopos-mobile-tmp" -Recurse -Force
```

Expected: ketiga folder ada di repo; folder sementara terhapus.

- [ ] **Step 2: Tambahkan paket ke `Packages/manifest.json`**

Tambahkan ke objek `dependencies` (biarkan modul `com.unity.modules.*` bawaan):

```json
"com.unity.xr.arfoundation": "6.3.4",
"com.unity.xr.arcore": "6.3.4",
"com.unity.inputsystem": "1.19.0",
"com.unity.ugui": "2.0.0",
"com.unity.test-framework": "1.6.0",
"com.unity.nuget.newtonsoft-json": "3.2.2"
```

Bila manifest bawaan sudah berisi salah satunya dengan versi lain, ganti dengan versi di atas.

- [ ] **Step 3: Berkas repo**

`.gitignore`:

```text
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
.vs/
.vscode/
.idea/
*.csproj
*.sln
*.apk
*.aab
mono_crash*
TestResults*.xml
*.local.json
*.local.json.meta
device-logs/
```

`LICENSE`: salin persis dari `D:\Dev\Projects\eutopos-vps\LICENSE` (teks AGPL-3.0).

```powershell
Copy-Item "D:\Dev\Projects\eutopos-vps\LICENSE" "D:\Dev\Projects\eutopos-mobile\LICENSE"
```

`README.md` (tulis ulang sebagai UTF-8, menggantikan berkas UTF-16):

```markdown
# eutopos-mobile

Aplikasi Android (Unity + ARCore) untuk indoor navigation dengan layanan VPS lokal
[eutopos-vps](https://github.com/RockHead07/eutopos-vps). Ponsel mengirim satu foto ke layanan,
menerima posisi dan arah hadap dalam kerangka gedung, lalu ARCore melacak gerak di antara koreksi.

Status: MVP satu lantai dalam pengembangan. Spesifikasi: `docs/specs/2026-10-15-mvp-navigasi-design.md`.

## Menyiapkan

1. Unity 6000.3.14f1 dengan modul Android.
2. Salin `vps-token.example.json` menjadi `Assets/Eutopos/Resources/vps-token.local.json` dan isi
   Client ID dan Secret service token Cloudflare Access. Berkas itu tidak pernah di-commit.

## Lisensi

Copyright (C) 2026 Bagus Insan Pradana. GNU Affero General Public License v3.0, lihat `LICENSE`.
```

`vps-token.example.json` (di akar repo, aman di-commit):

```json
{
  "base_url": "https://eutopos.rockhead07.tech",
  "client_id": "ISI-CLIENT-ID.access",
  "client_secret": "ISI-CLIENT-SECRET"
}
```

- [ ] **Step 4: Assembly definition**

`Assets/Eutopos/Scripts/Core/Eutopos.Core.asmdef`:

```json
{
  "name": "Eutopos.Core",
  "rootNamespace": "Eutopos.Core",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["Newtonsoft.Json.dll"],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
```

`Assets/Eutopos/Tests/EditMode/Eutopos.Core.Tests.asmdef`:

```json
{
  "name": "Eutopos.Core.Tests",
  "rootNamespace": "Eutopos.Core.Tests",
  "references": ["Eutopos.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll", "Newtonsoft.Json.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 5: Tes asap**

`Assets/Eutopos/Tests/EditMode/SmokeTests.cs`:

```csharp
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void SystemNumericsIsAvailable()
        {
            Assert.That(Vector3.Transform(Vector3.UnitZ, Quaternion.Identity), Is.EqualTo(Vector3.UnitZ));
        }
    }
}
```

- [ ] **Step 6: Jalankan tes EditMode (perintah ini dipakai semua task)**

Tutup Unity Editor untuk proyek ini dulu, lalu:

```powershell
& "D:\Dev\Tools\Unity Editors\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\Dev\Projects\eutopos-mobile" -runTests -testPlatform EditMode -testResults "D:\Dev\Projects\eutopos-mobile\TestResults-EditMode.xml" -logFile -
echo "exit $LASTEXITCODE"
```

Expected: `exit 0` dan `TestResults-EditMode.xml` berisi `result="Passed"` untuk `SystemNumericsIsAvailable`. (Unity keluar dengan 2 bila ada tes gagal, 3 bila galat menjalankan.)

- [ ] **Step 7: Commit (titik commit yang diusulkan)**

```bash
git add .gitignore LICENSE README.md vps-token.example.json Assets Packages ProjectSettings
git commit -m "chore: Unity 6000.3 project with AR Foundation, ARCore and core assemblies"
```

### Task 2: Konversi kerangka, intrinsik, dan pemutaran citra

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/FrameConvert.cs`
- Create: `Assets/Eutopos/Scripts/Core/Intrinsics.cs`
- Create: `Assets/Eutopos/Scripts/Core/ImageOps.cs`
- Test: `Assets/Eutopos/Tests/EditMode/FrameConvertTests.cs`

**Interfaces:**
- Produces:
  - `FrameConvert.GToB(Vector3) -> Vector3`, `FrameConvert.BToG(Vector3) -> Vector3`
  - `FrameConvert.ForwardB(Quaternion buildingFromCamG) -> Vector3`
  - `FrameConvert.YawDeg(Vector3 forward) -> float?` (null bila komponen mendatar < `MinHorizontalFraction` = 0.3)
  - `FrameConvert.WrapDeg(float) -> float` (hasil di [−180, 180)), `FrameConvert.YawDiffDeg(float a, float b) -> float`
  - `struct Intrinsics(float fx, float fy, float cx, float cy, int width, int height)` dengan `RotatedClockwise()`, `ScaledTo(int width, int height)`, `Project(Vector3) -> Vector2`
  - `ImageOps.RotateClockwiseBottomUp(byte[] rgbaTopDown, int width, int height) -> byte[]`

- [ ] **Step 1: Tulis tes yang gagal**

```csharp
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class FrameConvertTests
    {
        const float Eps = 1e-4f;

        static void Near(Vector3 a, Vector3 b) =>
            Assert.That(Vector3.Distance(a, b), Is.LessThan(Eps), $"{a} != {b}");

        [Test]
        public void GToBSwapsYAndZ()
        {
            Near(FrameConvert.GToB(new Vector3(1, 2, 3)), new Vector3(1, 3, 2));
            Near(FrameConvert.BToG(FrameConvert.GToB(new Vector3(1, 2, 3))), new Vector3(1, 2, 3));
        }

        [Test]
        public void CameraLookingAlongPlusXInGHasYaw90InB()
        {
            // kamera COLMAP: z depan. Putar 90 derajat di sekitar y_G supaya z_kamera = +x_G
            var r = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathFPi() / 2);
            var f = FrameConvert.ForwardB(r);
            Near(f, new Vector3(1, 0, 0));
            Assert.That(FrameConvert.YawDeg(f).Value, Is.EqualTo(90f).Within(Eps));
        }

        [Test]
        public void CameraLookingAlongPlusYInGHasYaw0InB()
        {
            // z_kamera = +y_G: putar -90 derajat di sekitar x_G
            var r = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathFPi() / 2);
            var f = FrameConvert.ForwardB(r);
            Near(f, new Vector3(0, 0, 1));
            Assert.That(FrameConvert.YawDeg(f).Value, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void SteepCameraHasNoYaw()
        {
            Assert.That(FrameConvert.YawDeg(new Vector3(0.1f, -1f, 0.1f)), Is.Null);
            Assert.That(FrameConvert.YawDeg(Vector3.Zero), Is.Null);
            Assert.That(FrameConvert.YawDeg(new Vector3(0.5f, -0.5f, 0.5f)), Is.Not.Null);
        }

        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(540f, -180f)]
        [TestCase(0f, 0f)]
        public void WrapDegStaysInRange(float input, float expected)
        {
            Assert.That(FrameConvert.WrapDeg(input), Is.EqualTo(expected).Within(Eps));
        }

        [Test]
        public void YawDiffCrossesTheSeam()
        {
            Assert.That(FrameConvert.YawDiffDeg(170f, -170f), Is.EqualTo(20f).Within(Eps));
        }

        [Test]
        public void RotatedIntrinsicsMatchARotatedImage()
        {
            var k = new Intrinsics(1000f, 990f, 960f, 540f, 1920, 1080);
            var p = new Vector3(0.3f, -0.2f, 2.0f); // titik di depan kamera sensor
            var uv = k.Project(p);
            // citra diputar 90 derajat searah jarum jam: u' = H - v, v' = u; kamera ikut: X' = -Y, Y' = X
            var kr = k.RotatedClockwise();
            var uvr = kr.Project(new Vector3(-p.Y, p.X, p.Z));
            Assert.That(uvr.X, Is.EqualTo(k.Height - uv.Y).Within(1e-3f));
            Assert.That(uvr.Y, Is.EqualTo(uv.X).Within(1e-3f));
            Assert.That((kr.Width, kr.Height), Is.EqualTo((1080, 1920)));
        }

        [Test]
        public void ScaledIntrinsicsFollowTheImageSize()
        {
            var k = new Intrinsics(1000f, 1000f, 960f, 540f, 1920, 1080).ScaledTo(960, 540);
            Assert.That((k.Fx, k.Cx, k.Cy, k.Width), Is.EqualTo((500f, 480f, 270f, 960)));
        }

        [Test]
        public void ImageRotationIsClockwiseAndBottomUp()
        {
            // citra 3x2 atas-ke-bawah: [A B C] / [D E F], satu kanal bermakna per piksel
            byte[] src = Px('A', 'B', 'C', 'D', 'E', 'F');
            // putar CW -> 2x3: [D A] / [E B] / [F C]; disimpan bawah-ke-atas untuk Texture2D
            byte[] dst = ImageOps.RotateClockwiseBottomUp(src, 3, 2);
            Assert.That(Letters(dst), Is.EqualTo("FCEBDA"));
        }

        static byte[] Px(params char[] letters)
        {
            var b = new byte[letters.Length * 4];
            for (int i = 0; i < letters.Length; i++) b[i * 4] = (byte)letters[i];
            return b;
        }

        static string Letters(byte[] rgba)
        {
            var s = new char[rgba.Length / 4];
            for (int i = 0; i < s.Length; i++) s[i] = (char)rgba[i * 4];
            return new string(s);
        }

        static float MathFPi() => (float)System.Math.PI;
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: kompilasi gagal (`FrameConvert`, `Intrinsics`, `ImageOps` tidak ada), exit bukan 0.

- [ ] **Step 3: Implementasi**

`Assets/Eutopos/Scripts/Core/FrameConvert.cs`:

```csharp
using System;
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>
    /// Kerangka G (server: tangan kanan, z atas) ke B (Unity: tangan kiri, Y atas), spesifikasi bagian 5.
    /// </summary>
    public static class FrameConvert
    {
        public const float MinHorizontalFraction = 0.3f;

        public static Vector3 GToB(Vector3 g) => new Vector3(g.X, g.Z, g.Y);

        public static Vector3 BToG(Vector3 b) => new Vector3(b.X, b.Z, b.Y);

        /// <summary>Sumbu optik kamera COLMAP (z depan) dari rotasi gedung-dari-kamera di G, dinyatakan di B.</summary>
        public static Vector3 ForwardB(Quaternion buildingFromCamG) =>
            GToB(Vector3.Transform(Vector3.UnitZ, buildingFromCamG));

        /// <summary>Yaw dari +Z ke +X (derajat), null bila kamera nyaris tegak lurus lantai.</summary>
        public static float? YawDeg(Vector3 f)
        {
            float len = f.Length();
            float horizontal = MathF.Sqrt(f.X * f.X + f.Z * f.Z);
            if (len < 1e-6f || horizontal < MinHorizontalFraction * len) return null;
            return MathF.Atan2(f.X, f.Z) * 180f / MathF.PI;
        }

        public static float WrapDeg(float deg)
        {
            float d = (deg + 180f) % 360f;
            if (d < 0) d += 360f;
            return d - 180f;
        }

        public static float YawDiffDeg(float a, float b) => MathF.Abs(WrapDeg(a - b));
    }
}
```

`Assets/Eutopos/Scripts/Core/Intrinsics.cs`:

```csharp
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>Intrinsik pinhole dalam piksel untuk citra Width x Height.</summary>
    public readonly struct Intrinsics
    {
        public readonly float Fx, Fy, Cx, Cy;
        public readonly int Width, Height;

        public Intrinsics(float fx, float fy, float cx, float cy, int width, int height)
        {
            Fx = fx; Fy = fy; Cx = cx; Cy = cy; Width = width; Height = height;
        }

        /// <summary>Intrinsik setelah citra diputar 90 derajat searah jarum jam (W x H menjadi H x W).</summary>
        public Intrinsics RotatedClockwise() => new Intrinsics(Fy, Fx, Height - Cy, Cx, Height, Width);

        public Intrinsics ScaledTo(int width, int height)
        {
            float sx = (float)width / Width, sy = (float)height / Height;
            return new Intrinsics(Fx * sx, Fy * sy, Cx * sx, Cy * sy, width, height);
        }

        public Vector2 Project(Vector3 p) => new Vector2(Fx * p.X / p.Z + Cx, Fy * p.Y / p.Z + Cy);
    }
}
```

`Assets/Eutopos/Scripts/Core/ImageOps.cs`:

```csharp
using System;

namespace Eutopos.Core
{
    public static class ImageOps
    {
        /// <summary>
        /// Putar citra RGBA (baris atas ke bawah, W x H) 90 derajat searah jarum jam. Hasil H x W disimpan
        /// baris bawah ke atas, sesuai Texture2D.LoadRawTextureData, supaya JPEG yang dikodekan tegak.
        /// </summary>
        public static byte[] RotateClockwiseBottomUp(byte[] src, int width, int height)
        {
            if (src.Length != width * height * 4) throw new ArgumentException("ukuran buffer tidak cocok");
            int nw = height, nh = width;
            var dst = new byte[src.Length];
            for (int vp = 0; vp < nh; vp++)
            {
                for (int up = 0; up < nw; up++)
                {
                    int u = vp, v = height - 1 - up;               // piksel sumber
                    int s = (v * width + u) * 4;
                    int d = ((nh - 1 - vp) * nw + up) * 4;         // tujuan, baris bawah ke atas
                    dst[d] = src[s]; dst[d + 1] = src[s + 1]; dst[d + 2] = src[s + 2]; dst[d + 3] = src[s + 3];
                }
            }
            return dst;
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lolos**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`, semua tes `FrameConvertTests` Passed.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/Core Assets/Eutopos/Tests/EditMode
git commit -m "feat(core): building frame conversion, yaw, intrinsics and image rotation"
```

### Task 3: Penyelarasan 4-DoF

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Aligner4Dof.cs`
- Test: `Assets/Eutopos/Tests/EditMode/Aligner4DofTests.cs`

**Interfaces:**
- Consumes: `FrameConvert.YawDeg`, `FrameConvert.WrapDeg` (Task 2).
- Produces: `readonly struct Alignment(float yawDeg, Vector3 translation)` dengan `YawDeg`, `Translation`, `BToU(Vector3) -> Vector3`, `UToB(Vector3) -> Vector3`; `Aligner4Dof.RotY(float deg, Vector3 v) -> Vector3` (sama dengan `Quaternion.Euler(0, deg, 0)` di Unity); `Aligner4Dof.Solve(Vector3 camPosU, Vector3 camFwdU, Vector3 camPosB, Vector3 camFwdB) -> Alignment?`.

- [ ] **Step 1: Tulis tes yang gagal**

```csharp
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class Aligner4DofTests
    {
        const float Eps = 1e-3f;

        static void Near(Vector3 a, Vector3 b) =>
            Assert.That(Vector3.Distance(a, b), Is.LessThan(Eps), $"{a} != {b}");

        [Test]
        public void RotYMatchesUnityConvention()
        {
            Near(Aligner4Dof.RotY(90f, new Vector3(0, 0, 1)), new Vector3(1, 0, 0));
            Near(Aligner4Dof.RotY(90f, new Vector3(1, 0, 0)), new Vector3(0, 0, -1));
            Near(Aligner4Dof.RotY(45f, new Vector3(0, 2, 0)), new Vector3(0, 2, 0));
        }

        [TestCase(30f, 1f, 0.2f, -4f)]
        [TestCase(-120f, -7.5f, 1.4f, 2f)]
        [TestCase(179f, 0f, 0f, 0f)]
        public void SolveRecoversTheTrueAlignment(float yaw, float tx, float ty, float tz)
        {
            var truth = new Alignment(yaw, new Vector3(tx, ty, tz));
            var camPosB = new Vector3(4.5f, 1.3f, 1.6f);
            var camFwdB = Vector3.Normalize(new Vector3(0.6f, -0.2f, 0.8f));
            var camPosU = truth.BToU(camPosB);
            var camFwdU = Aligner4Dof.RotY(yaw, camFwdB);

            var got = Aligner4Dof.Solve(camPosU, camFwdU, camPosB, camFwdB);

            Assert.That(got, Is.Not.Null);
            Assert.That(FrameConvert.YawDiffDeg(got.Value.YawDeg, yaw), Is.LessThan(Eps));
            Near(got.Value.Translation, truth.Translation);
        }

        [Test]
        public void YawAcrossTheSeamIsTheShortWay()
        {
            var got = Aligner4Dof.Solve(
                Vector3.Zero, Aligner4Dof.RotY(170f, Vector3.UnitZ),
                Vector3.Zero, Aligner4Dof.RotY(-170f, Vector3.UnitZ));
            Assert.That(got.Value.YawDeg, Is.EqualTo(-20f).Within(Eps));
        }

        [Test]
        public void RoundTripBetweenFrames()
        {
            var a = new Alignment(63f, new Vector3(2, -1, 5));
            var p = new Vector3(3, 0.4f, -2);
            Near(a.UToB(a.BToU(p)), p);
        }

        [Test]
        public void SteepCameraCannotBeAligned()
        {
            Assert.That(Aligner4Dof.Solve(Vector3.Zero, -Vector3.UnitY, Vector3.Zero, Vector3.UnitZ), Is.Null);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: kompilasi gagal (`Alignment`, `Aligner4Dof` tidak ada).

- [ ] **Step 3: Implementasi**

```csharp
using System;
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>Kerangka B di U: p_U = RotY(YawDeg) * p_B + Translation (spesifikasi bagian 5).</summary>
    public readonly struct Alignment
    {
        public readonly float YawDeg;
        public readonly Vector3 Translation;

        public Alignment(float yawDeg, Vector3 translation)
        {
            YawDeg = FrameConvert.WrapDeg(yawDeg);
            Translation = translation;
        }

        public Vector3 BToU(Vector3 pB) => Aligner4Dof.RotY(YawDeg, pB) + Translation;

        public Vector3 UToB(Vector3 pU) => Aligner4Dof.RotY(-YawDeg, pU - Translation);
    }

    public static class Aligner4Dof
    {
        /// <summary>Putar di sekitar +Y, sama dengan Quaternion.Euler(0, deg, 0) di Unity (+Z ke +X).</summary>
        public static Vector3 RotY(float deg, Vector3 v)
        {
            float r = deg * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
            return new Vector3(c * v.X + s * v.Z, v.Y, -s * v.X + c * v.Z);
        }

        public static Alignment? Solve(Vector3 camPosU, Vector3 camFwdU, Vector3 camPosB, Vector3 camFwdB)
        {
            float? yawU = FrameConvert.YawDeg(camFwdU), yawB = FrameConvert.YawDeg(camFwdB);
            if (yawU == null || yawB == null) return null;
            float theta = FrameConvert.WrapDeg(yawU.Value - yawB.Value);
            return new Alignment(theta, camPosU - RotY(theta, camPosB));
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lolos**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/Core/Aligner4Dof.cs Assets/Eutopos/Tests/EditMode/Aligner4DofTests.cs
git commit -m "feat(core): 4-DoF alignment between the ARCore session and the building frame"
```

### Task 4: Penjaga lompatan dan penjadwal koreksi

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/JumpGuard.cs`
- Create: `Assets/Eutopos/Scripts/Core/CorrectionScheduler.cs`
- Test: `Assets/Eutopos/Tests/EditMode/GuardAndSchedulerTests.cs`

**Interfaces:**
- Consumes: `Alignment` (Task 3).
- Produces:
  - `enum CorrectionDecision { Accepted, Confirmed, Candidate }`
  - `readonly struct GuardResult(CorrectionDecision decision, float jumpM, bool droppedCandidate)`
  - `class JumpGuard` dengan field publik `JumpThresholdM = 3f`, `RecentS = 30f`, `ConfirmM = 1f`, properti `HasCandidate`, metode `GuardResult Evaluate(float t, Alignment? current, Alignment proposed, Vector3 camPosU)`
  - `class CorrectionScheduler` dengan field `IntervalS = 5f`, properti `Navigating`, `Tracking`, `InFlight`, metode `bool ShouldFireTimer(float t)`, `bool TryStart(float t)`, `void Finish()`

- [ ] **Step 1: Tulis tes yang gagal**

```csharp
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class GuardAndSchedulerTests
    {
        static readonly Alignment Here = new Alignment(0f, Vector3.Zero);
        static Alignment Shifted(float metersX) => new Alignment(0f, new Vector3(-metersX, 0, 0));
        static readonly Vector3 Cam = new Vector3(1, 1.3f, 1);

        [Test]
        public void FirstCorrectionIsAlwaysAccepted()
        {
            var g = new JumpGuard();
            Assert.That(g.Evaluate(0f, null, Shifted(50f), Cam).Decision, Is.EqualTo(CorrectionDecision.Accepted));
        }

        [Test]
        public void SmallJumpIsAccepted()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            var r = g.Evaluate(5f, Here, Shifted(0.5f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Accepted));
            Assert.That(r.JumpM, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void BigRecentJumpBecomesACandidateThenIsConfirmed()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            Assert.That(g.Evaluate(5f, Here, Shifted(6f), Cam).Decision, Is.EqualTo(CorrectionDecision.Candidate));
            Assert.That(g.HasCandidate, Is.True);
            // koreksi berikutnya setuju dengan kandidat (selisih 0,4 m < 1 m)
            var r = g.Evaluate(10f, Here, Shifted(6.4f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Confirmed));
            Assert.That(g.HasCandidate, Is.False);
        }

        [Test]
        public void CandidateIsDroppedWhenTheNextCorrectionDisagrees()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            g.Evaluate(5f, Here, Shifted(6f), Cam);
            var r = g.Evaluate(10f, Here, Shifted(0.2f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Accepted));
            Assert.That(r.DroppedCandidate, Is.True);
            Assert.That(g.HasCandidate, Is.False);
        }

        [Test]
        public void BigJumpAfterALongGapIsAccepted()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            Assert.That(g.Evaluate(31f, Here, Shifted(6f), Cam).Decision, Is.EqualTo(CorrectionDecision.Accepted));
        }

        [Test]
        public void TimerNeverFiresWhileARequestIsInFlightOrTrackingIsLost()
        {
            var s = new CorrectionScheduler { Navigating = true, Tracking = true };
            Assert.That(s.ShouldFireTimer(0f), Is.True);
            Assert.That(s.TryStart(0f), Is.True);
            Assert.That(s.ShouldFireTimer(100f), Is.False);   // masih berjalan
            Assert.That(s.TryStart(100f), Is.False);          // tombol juga ditolak
            s.Finish();
            Assert.That(s.ShouldFireTimer(3f), Is.False);     // belum 5 detik sejak mulai
            Assert.That(s.ShouldFireTimer(5f), Is.True);
            s.Tracking = false;
            Assert.That(s.ShouldFireTimer(50f), Is.False);
            Assert.That(s.TryStart(50f), Is.False);
        }

        [Test]
        public void TimerIsSilentWhenNotNavigatingButTheButtonWorks()
        {
            var s = new CorrectionScheduler { Navigating = false, Tracking = true };
            Assert.That(s.ShouldFireTimer(100f), Is.False);
            Assert.That(s.TryStart(100f), Is.True);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: kompilasi gagal (`JumpGuard`, `CorrectionScheduler` tidak ada).

- [ ] **Step 3: Implementasi**

`Assets/Eutopos/Scripts/Core/JumpGuard.cs`:

```csharp
using System;
using System.Numerics;

namespace Eutopos.Core
{
    public enum CorrectionDecision { Accepted, Confirmed, Candidate }

    public readonly struct GuardResult
    {
        public readonly CorrectionDecision Decision;
        public readonly float JumpM;
        public readonly bool DroppedCandidate;

        public GuardResult(CorrectionDecision decision, float jumpM, bool droppedCandidate)
        {
            Decision = decision; JumpM = jumpM; DroppedCandidate = droppedCandidate;
        }
    }

    /// <summary>
    /// Mencegah satu koreksi "salah yakin" memindahkan pengguna (spesifikasi bagian 9). Lompatan besar yang
    /// datang tak lama setelah koreksi diterima ditahan sebagai kandidat sampai koreksi berikutnya setuju.
    /// Kandidat dibandingkan sebagai penyelarasan, bukan posisi, karena pengguna sudah berjalan.
    /// </summary>
    public sealed class JumpGuard
    {
        public float JumpThresholdM = 3f;
        public float RecentS = 30f;
        public float ConfirmM = 1f;

        Alignment? candidate;
        float lastAcceptedT = float.NegativeInfinity;

        public bool HasCandidate => candidate.HasValue;

        public GuardResult Evaluate(float t, Alignment? current, Alignment proposed, Vector3 camPosU)
        {
            Vector3 measuredB = proposed.UToB(camPosU);
            if (current == null)
            {
                lastAcceptedT = t;
                return new GuardResult(CorrectionDecision.Accepted, 0f, false);
            }
            float jump = Horizontal(current.Value.UToB(camPosU), measuredB);
            bool dropped = false;
            if (candidate.HasValue)
            {
                float d = Horizontal(candidate.Value.UToB(camPosU), measuredB);
                candidate = null;
                if (d < ConfirmM)
                {
                    lastAcceptedT = t;
                    return new GuardResult(CorrectionDecision.Confirmed, jump, false);
                }
                dropped = true;
            }
            if (jump > JumpThresholdM && t - lastAcceptedT < RecentS)
            {
                candidate = proposed;
                return new GuardResult(CorrectionDecision.Candidate, jump, dropped);
            }
            lastAcceptedT = t;
            return new GuardResult(CorrectionDecision.Accepted, jump, dropped);
        }

        static float Horizontal(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
}
```

`Assets/Eutopos/Scripts/Core/CorrectionScheduler.cs`:

```csharp
namespace Eutopos.Core
{
    /// <summary>Satu permintaan koreksi dalam satu waktu; timer hanya saat navigasi dan pelacakan aktif.</summary>
    public sealed class CorrectionScheduler
    {
        public float IntervalS = 5f;
        public bool Navigating { get; set; }
        public bool Tracking { get; set; }
        public bool InFlight { get; private set; }

        float lastStartT = float.NegativeInfinity;

        public bool ShouldFireTimer(float t) => Navigating && Tracking && !InFlight && t - lastStartT >= IntervalS;

        public bool TryStart(float t)
        {
            if (InFlight || !Tracking) return false;
            InFlight = true;
            lastStartT = t;
            return true;
        }

        public void Finish() => InFlight = false;
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lolos**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/Core/JumpGuard.cs Assets/Eutopos/Scripts/Core/CorrectionScheduler.cs Assets/Eutopos/Tests/EditMode/GuardAndSchedulerTests.cs
git commit -m "feat(core): jump guard for corrections and a single-flight correction scheduler"
```

### Task 5: Graf navigasi dan A*

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/NavGraph.cs`
- Test: `Assets/Eutopos/Tests/EditMode/NavGraphTests.cs`

**Interfaces:**
- Produces:
  - `class NavTarget` (`Node`, `Name`)
  - `class Route` (`IReadOnlyList<Vector2> Points` dalam G: posisi pengguna, titik proyeksi, lalu simpul; `IReadOnlyList<string> NodeIds`; `float LengthM`)
  - `class NavGraph` dengan `static NavGraph Parse(string json)` (FormatException), `IReadOnlyDictionary<string, Vector2> Nodes`, `IReadOnlyList<NavTarget> Targets`, `bool TryProject(Vector2 p, out Vector2 point, out string a, out string b)`, `Route Plan(Vector2 from, string targetNode)` (null bila tak terjangkau), `static bool Arrived(Vector2 user, Vector2 target, float radiusM = 1.0f)`

- [ ] **Step 1: Tulis tes yang gagal**

```csharp
using System;
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class NavGraphTests
    {
        // Lorong L: P01 (0,0) - P02 (4,0) - P03 (4,3); P04 terpisah di (10,10)
        const string Good = @"{
          ""frame"": ""building"", ""units"": ""m"",
          ""nodes"": [{""id"":""P01"",""x"":0,""y"":0},{""id"":""P02"",""x"":4,""y"":0},
                      {""id"":""P03"",""x"":4,""y"":3},{""id"":""P04"",""x"":10,""y"":10}],
          ""edges"": [[""P01"",""P02""],[""P02"",""P03""]],
          ""targets"": [{""node"":""P03"",""name"":""Pintu Lab""},{""node"":""P04"",""name"":""Pulau""}]
        }";

        [Test]
        public void ParsesNodesAndTargets()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.Nodes.Count, Is.EqualTo(4));
            Assert.That(g.Nodes["P02"], Is.EqualTo(new Vector2(4, 0)));
            Assert.That(g.Targets[0].Name, Is.EqualTo("Pintu Lab"));
        }

        [TestCase(@"{""frame"":""model"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[]}", "frame")]
        [TestCase(@"{""frame"":""building"",""units"":""cm"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[]}", "units")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[],""edges"":[],""targets"":[]}", "nodes")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0},{""id"":""A"",""x"":1,""y"":0}],""edges"":[],""targets"":[]}", "ganda")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":""0"",""y"":0}],""edges"":[],""targets"":[]}", "bukan angka")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[[""A"",""Z""]],""targets"":[]}", "Z")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[[""A"",""A""]],""targets"":[]}", "dirinya")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[{""node"":""Z"",""name"":""x""}]}", "target")]
        [TestCase(@"{bukan json", "JSON")]
        public void RejectsBrokenGraphsWithTheReason(string json, string words)
        {
            var e = Assert.Throws<FormatException>(() => NavGraph.Parse(json));
            StringAssert.Contains(words, e.Message);
        }

        [Test]
        public void ProjectsOntoTheNearestEdge()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.TryProject(new Vector2(2, 0.7f), out var p, out var a, out var b), Is.True);
            Assert.That(Vector2.Distance(p, new Vector2(2, 0)), Is.LessThan(1e-4f));
            Assert.That((a, b), Is.EqualTo(("P01", "P02")));
        }

        [Test]
        public void PlansAlongTheCorridor()
        {
            var r = NavGraph.Parse(Good).Plan(new Vector2(1, 0.5f), "P03");
            Assert.That(r, Is.Not.Null);
            Assert.That(r.NodeIds, Is.EqualTo(new[] { "P02", "P03" }));
            Assert.That(r.Points[0], Is.EqualTo(new Vector2(1, 0.5f)));
            Assert.That(Vector2.Distance(r.Points[1], new Vector2(1, 0)), Is.LessThan(1e-4f));
            Assert.That(r.LengthM, Is.EqualTo(0.5f + 3f + 3f).Within(1e-3f));
        }

        [Test]
        public void UnreachableTargetHasNoRoute()
        {
            Assert.That(NavGraph.Parse(Good).Plan(new Vector2(1, 0), "P04"), Is.Null);
        }

        [Test]
        public void UnknownTargetIsAnError()
        {
            Assert.Throws<ArgumentException>(() => NavGraph.Parse(Good).Plan(Vector2.Zero, "P99"));
        }

        [Test]
        public void ArrivalUsesAStrictRadius()
        {
            Assert.That(NavGraph.Arrived(new Vector2(0.99f, 0), Vector2.Zero), Is.True);
            Assert.That(NavGraph.Arrived(new Vector2(1.0f, 0), Vector2.Zero), Is.False);
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: kompilasi gagal (`NavGraph` tidak ada).

- [ ] **Step 3: Implementasi**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core
{
    public sealed class NavTarget
    {
        public string Node { get; }
        public string Name { get; }
        public NavTarget(string node, string name) { Node = node; Name = name; }
    }

    public sealed class Route
    {
        public IReadOnlyList<Vector2> Points { get; }
        public IReadOnlyList<string> NodeIds { get; }
        public float LengthM { get; }

        public Route(List<Vector2> points, List<string> nodeIds)
        {
            Points = points;
            NodeIds = nodeIds;
            float len = 0f;
            for (int i = 1; i < points.Count; i++) len += Vector2.Distance(points[i - 1], points[i]);
            LengthM = len;
        }
    }

    /// <summary>Graf titik terukur dalam kerangka G (x, y meter), spesifikasi bagian 7.</summary>
    public sealed class NavGraph
    {
        readonly Dictionary<string, Vector2> nodes = new Dictionary<string, Vector2>();
        readonly Dictionary<string, List<string>> adj = new Dictionary<string, List<string>>();
        readonly List<(string a, string b)> edges = new List<(string a, string b)>();
        readonly List<NavTarget> targets = new List<NavTarget>();

        public IReadOnlyDictionary<string, Vector2> Nodes => nodes;
        public IReadOnlyList<NavTarget> Targets => targets;

        NavGraph() { }

        public static NavGraph Parse(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonReaderException e) { throw new FormatException("navgraph bukan JSON: " + e.Message); }

            if ((string)root["frame"] != "building") throw new FormatException("frame harus \"building\"");
            if ((string)root["units"] != "m") throw new FormatException("units harus \"m\"");
            var g = new NavGraph();
            if (!(root["nodes"] is JArray nodeArr) || nodeArr.Count == 0) throw new FormatException("nodes kosong");
            foreach (var n in nodeArr)
            {
                var id = (string)n["id"];
                if (string.IsNullOrEmpty(id)) throw new FormatException("simpul tanpa id");
                if (g.nodes.ContainsKey(id)) throw new FormatException("id simpul ganda: " + id);
                g.nodes[id] = new Vector2(Number(n["x"], id, "x"), Number(n["y"], id, "y"));
                g.adj[id] = new List<string>();
            }
            if (!(root["edges"] is JArray edgeArr)) throw new FormatException("edges harus daftar");
            foreach (var e in edgeArr)
            {
                if (!(e is JArray pair) || pair.Count != 2 || pair[0].Type != JTokenType.String || pair[1].Type != JTokenType.String)
                    throw new FormatException("sisi harus pasangan id: " + e.ToString(Formatting.None));
                string a = (string)pair[0], b = (string)pair[1];
                if (a == b) throw new FormatException("sisi ke dirinya sendiri: " + a);
                foreach (var id in new[] { a, b })
                    if (!g.nodes.ContainsKey(id)) throw new FormatException("sisi merujuk simpul yang tidak ada: " + id);
                g.edges.Add((a, b));
                g.adj[a].Add(b);
                g.adj[b].Add(a);
            }
            if (!(root["targets"] is JArray targetArr)) throw new FormatException("targets harus daftar");
            foreach (var t in targetArr)
            {
                var node = (string)t["node"];
                var name = (string)t["name"];
                if (node == null || !g.nodes.ContainsKey(node) || string.IsNullOrEmpty(name))
                    throw new FormatException("target tidak sah: " + t.ToString(Formatting.None));
                g.targets.Add(new NavTarget(node, name));
            }
            return g;
        }

        static float Number(JToken token, string id, string key)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new FormatException($"simpul {id}: {key} bukan angka");
            double v = (double)token;
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new FormatException($"simpul {id}: {key} bukan angka");
            return (float)v;
        }

        public bool TryProject(Vector2 p, out Vector2 point, out string a, out string b)
        {
            float best = float.PositiveInfinity;
            point = default; a = null; b = null;
            foreach (var (ea, eb) in edges)
            {
                Vector2 A = nodes[ea], B = nodes[eb], ab = B - A;
                float len2 = ab.LengthSquared();
                float s = len2 > 0 ? Math.Clamp(Vector2.Dot(p - A, ab) / len2, 0f, 1f) : 0f;
                Vector2 q = A + s * ab;
                float d = Vector2.DistanceSquared(p, q);
                if (d < best) { best = d; point = q; a = ea; b = eb; }
            }
            return a != null;
        }

        /// <summary>A* dari titik proyeksi pengguna ke simpul target. Null bila tidak terjangkau.</summary>
        public Route Plan(Vector2 from, string targetNode)
        {
            if (!nodes.ContainsKey(targetNode)) throw new ArgumentException("target tidak ada: " + targetNode);
            if (!TryProject(from, out var proj, out var ea, out var eb)) return null;

            const string Start = "\u0000start";
            Vector2 Pos(string id) => id == Start ? proj : nodes[id];
            IEnumerable<string> Neighbors(string id) => id == Start ? new[] { ea, eb } : (IEnumerable<string>)adj[id];
            Vector2 goal = nodes[targetNode];

            var cost = new Dictionary<string, float> { [Start] = 0f };
            var parent = new Dictionary<string, string>();
            var open = new List<string> { Start };
            var closed = new HashSet<string>();
            bool reached = false;
            while (open.Count > 0)
            {
                string cur = open[0];
                float bestF = float.PositiveInfinity;
                foreach (var o in open)
                {
                    float f = cost[o] + Vector2.Distance(Pos(o), goal);
                    if (f < bestF) { bestF = f; cur = o; }
                }
                if (cur == targetNode) { reached = true; break; }
                open.Remove(cur);
                closed.Add(cur);
                foreach (var nb in Neighbors(cur))
                {
                    if (closed.Contains(nb)) continue;
                    float c = cost[cur] + Vector2.Distance(Pos(cur), nodes[nb]);
                    if (!cost.TryGetValue(nb, out var old) || c < old)
                    {
                        cost[nb] = c;
                        parent[nb] = cur;
                        if (!open.Contains(nb)) open.Add(nb);
                    }
                }
            }
            if (!reached) return null;

            var ids = new List<string>();
            for (string c = targetNode; c != Start; c = parent[c]) ids.Add(c);
            ids.Reverse();
            var points = new List<Vector2> { from, proj };
            foreach (var id in ids) points.Add(nodes[id]);
            return new Route(points, ids);
        }

        public static bool Arrived(Vector2 user, Vector2 target, float radiusM = 1.0f) =>
            Vector2.Distance(user, target) < radiusM;
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lolos**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/Core/NavGraph.cs Assets/Eutopos/Tests/EditMode/NavGraphTests.cs
git commit -m "feat(core): navigation graph parsing, edge projection and A* routing"
```

### Task 6: Jawaban server dan format angka

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/VpsResponses.cs`
- Test: `Assets/Eutopos/Tests/EditMode/VpsResponsesTests.cs`

**Interfaces:**
- Consumes: `NavGraph.Parse` (Task 5).
- Produces:
  - `enum VpsStatus { Ok, Failed, NotAligned, NoMap, NotFound, Unauthorized, NetworkError, ServerError }`
  - `class LocalizeResult` (`Status`, `Inliers`, `PositionG` (Vector3), `RotationG` (Quaternion, gedung-dari-kamera), `AreaId`, `MapVersion` (int?), `TimingsS` (IReadOnlyDictionary<string, float>), `Detail`)
  - `class NavGraphResult` (`Status`, `Graph`, `AreaId`, `MapVersion`, `Detail`)
  - `VpsResponses.MinInliers = 50`, `VpsResponses.ClassifyLocalize(long httpCode, string body, bool connectionError) -> LocalizeResult`, `VpsResponses.ParseNavGraph(long httpCode, string body, bool connectionError) -> NavGraphResult`, `VpsResponses.Number(float) -> string` (budaya invarian)

- [ ] **Step 1: Tulis tes yang gagal**

```csharp
using System.Globalization;
using System.Numerics;
using System.Threading;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class VpsResponsesTests
    {
        const string Ok = @"{""status"":""ok"",""inliers"":120,""correspondences"":300,""intrinsics_source"":""client"",
          ""pose_model"":{""position"":[0,0,0],""rotation_xyzw"":[0,0,0,1]},
          ""pose_building"":{""position"":[1.5,2.0,1.3],""rotation_xyzw"":[0,0,0,1]},
          ""area_id"":""floor10"",""map_version"":4,""t_s"":{""total"":0.9}}";

        [Test]
        public void OkAnswerCarriesThePoseInTheBuildingFrame()
        {
            var r = VpsResponses.ClassifyLocalize(200, Ok, false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(r.PositionG, Is.EqualTo(new Vector3(1.5f, 2.0f, 1.3f)));
            Assert.That(r.RotationG, Is.EqualTo(Quaternion.Identity));
            Assert.That((r.Inliers, r.AreaId, r.MapVersion), Is.EqualTo((120, "floor10", (int?)4)));
            Assert.That(r.TimingsS["total"], Is.EqualTo(0.9f).Within(1e-6f));
        }

        [Test]
        public void FewInliersOrFailedStatusIsFailed()
        {
            Assert.That(VpsResponses.ClassifyLocalize(200, Ok.Replace("120", "49"), false).Status, Is.EqualTo(VpsStatus.Failed));
            Assert.That(VpsResponses.ClassifyLocalize(200, Ok.Replace(@"""ok""", @"""failed"""), false).Status, Is.EqualTo(VpsStatus.Failed));
        }

        [Test]
        public void MissingBuildingPoseMeansNotAligned()
        {
            var body = Ok.Replace(@"{""position"":[1.5,2.0,1.3],""rotation_xyzw"":[0,0,0,1]}", "null");
            Assert.That(VpsResponses.ClassifyLocalize(200, body, false).Status, Is.EqualTo(VpsStatus.NotAligned));
        }

        [TestCase(302)]
        [TestCase(401)]
        [TestCase(403)]
        public void AccessRedirectsAndRefusalsAreUnauthorized(long code)
        {
            var r = VpsResponses.ClassifyLocalize(code, "<html>login</html>", false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Unauthorized));
        }

        [Test]
        public void OtherOutcomes()
        {
            Assert.That(VpsResponses.ClassifyLocalize(503, "{}", false).Status, Is.EqualTo(VpsStatus.NoMap));
            Assert.That(VpsResponses.ClassifyLocalize(0, null, true).Status, Is.EqualTo(VpsStatus.NetworkError));
            Assert.That(VpsResponses.ClassifyLocalize(500, "boom", false).Status, Is.EqualTo(VpsStatus.ServerError));
            Assert.That(VpsResponses.ClassifyLocalize(200, "<html>", false).Status, Is.EqualTo(VpsStatus.ServerError));
        }

        [Test]
        public void NavGraphOutcomes()
        {
            const string graph = @"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0},{""id"":""B"",""x"":1,""y"":0}],""edges"":[[""A"",""B""]],""targets"":[{""node"":""B"",""name"":""Lab""}]}";
            var ok = VpsResponses.ParseNavGraph(200, @"{""area_id"":""floor10"",""map_version"":4,""graph"":" + graph + "}", false);
            Assert.That(ok.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(ok.Graph.Targets[0].Name, Is.EqualTo("Lab"));
            Assert.That(VpsResponses.ParseNavGraph(404, "{}", false).Status, Is.EqualTo(VpsStatus.NotFound));
            var broken = VpsResponses.ParseNavGraph(200, @"{""area_id"":""a"",""map_version"":1,""graph"":{""frame"":""x""}}", false);
            Assert.That(broken.Status, Is.EqualTo(VpsStatus.ServerError));
            StringAssert.Contains("frame", broken.Detail);
        }

        [Test]
        public void NumbersUseADotEvenOnAnIndonesianPhone()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("id-ID");
                Assert.That(VpsResponses.Number(1234.5f), Is.EqualTo("1234.5"));
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }
    }
}
```

- [ ] **Step 2: Jalankan tes, pastikan gagal**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: kompilasi gagal (`VpsResponses` tidak ada).

- [ ] **Step 3: Implementasi**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core
{
    public enum VpsStatus { Ok, Failed, NotAligned, NoMap, NotFound, Unauthorized, NetworkError, ServerError }

    public sealed class LocalizeResult
    {
        public VpsStatus Status;
        public int Inliers;
        public Vector3 PositionG;
        public Quaternion RotationG;
        public string AreaId;
        public int? MapVersion;
        public IReadOnlyDictionary<string, float> TimingsS = new Dictionary<string, float>();
        public string Detail;
    }

    public sealed class NavGraphResult
    {
        public VpsStatus Status;
        public NavGraph Graph;
        public string AreaId;
        public int? MapVersion;
        public string Detail;
    }

    public static class VpsResponses
    {
        public const int MinInliers = 50;

        public static string Number(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Status yang sama untuk semua endpoint. Cloudflare Access mengalihkan ke halaman login (3xx).</summary>
        static VpsStatus? Common(long code, bool connectionError)
        {
            if (code >= 300 && code < 400 || code == 401 || code == 403) return VpsStatus.Unauthorized;
            if (connectionError && code == 0) return VpsStatus.NetworkError;
            if (code == 503) return VpsStatus.NoMap;
            return null;
        }

        static string Short(string s) => s == null ? "" : (s.Length > 200 ? s.Substring(0, 200) : s);

        public static LocalizeResult ClassifyLocalize(long httpCode, string body, bool connectionError)
        {
            var common = Common(httpCode, connectionError);
            if (common != null) return new LocalizeResult { Status = common.Value, Detail = Short(body) };
            if (httpCode != 200) return new LocalizeResult { Status = VpsStatus.ServerError, Detail = $"HTTP {httpCode}: {Short(body)}" };
            JObject j;
            try { j = JObject.Parse(body); }
            catch (JsonReaderException) { return new LocalizeResult { Status = VpsStatus.ServerError, Detail = "jawaban bukan JSON" }; }

            var r = new LocalizeResult
            {
                Inliers = (int?)j["inliers"] ?? 0,
                AreaId = (string)j["area_id"],
                MapVersion = (int?)j["map_version"],
            };
            var timings = new Dictionary<string, float>();
            if (j["t_s"] is JObject ts)
                foreach (var p in ts.Properties()) timings[p.Name] = (float)p.Value;
            r.TimingsS = timings;

            if ((string)j["status"] != "ok" || r.Inliers < MinInliers) { r.Status = VpsStatus.Failed; return r; }
            if (!(j["pose_building"] is JObject pose)) { r.Status = VpsStatus.NotAligned; return r; }
            var pos = (JArray)pose["position"];
            var q = (JArray)pose["rotation_xyzw"];
            r.PositionG = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
            r.RotationG = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]);
            r.Status = VpsStatus.Ok;
            return r;
        }

        public static NavGraphResult ParseNavGraph(long httpCode, string body, bool connectionError)
        {
            var common = Common(httpCode, connectionError);
            if (common != null) return new NavGraphResult { Status = common.Value, Detail = Short(body) };
            if (httpCode == 404) return new NavGraphResult { Status = VpsStatus.NotFound, Detail = Short(body) };
            if (httpCode != 200) return new NavGraphResult { Status = VpsStatus.ServerError, Detail = $"HTTP {httpCode}: {Short(body)}" };
            try
            {
                var j = JObject.Parse(body);
                return new NavGraphResult
                {
                    Status = VpsStatus.Ok,
                    Graph = NavGraph.Parse(j["graph"].ToString(Formatting.None)),
                    AreaId = (string)j["area_id"],
                    MapVersion = (int?)j["map_version"],
                };
            }
            catch (Exception e) when (e is JsonReaderException || e is FormatException || e is NullReferenceException)
            {
                return new NavGraphResult { Status = VpsStatus.ServerError, Detail = "graf rusak: " + e.Message };
            }
        }
    }
}
```

- [ ] **Step 4: Jalankan tes, pastikan lolos**

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`, seluruh tes inti (Task 1 sampai 6) Passed.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/Core/VpsResponses.cs Assets/Eutopos/Tests/EditMode/VpsResponsesTests.cs
git commit -m "feat(core): classify VPS answers and keep numbers culture-invariant"
```

---

## Fase B: aplikasi di ponsel

Prasyarat fase ini: PR server (`/navgraph`, `manage align`) sudah merged dan ter-deploy, dan service token Cloudflare Access sudah dibuat Bagus. Lokalisasi tetap bisa diuji sebelum titik meteran ada, tetapi `pose_building` akan kosong (pesan "peta belum diselaraskan") sampai `align.json` dipasang.

### Task 7: Pengaturan Android, scene, dan build

**Files:**
- Create: `Assets/Eutopos/Editor/Eutopos.Editor.asmdef`
- Create: `Assets/Eutopos/Editor/ProjectSetup.cs`
- Create: `Assets/Eutopos/Editor/BuildAndroid.cs`
- Create: `Assets/Eutopos/Scenes/Main.unity` (lewat Editor)

**Interfaces:**
- Produces: menu `Eutopos/Atur Player Android` dan `Eutopos/Build APK`; metode batch `Eutopos.EditorTools.BuildAndroid.Build`; APK di `Builds/eutopos-mobile.apk`; scene berisi `AR Session`, `XR Origin (Mobile AR)` dengan `ARAnchorManager`, dan objek `App`.

- [ ] **Step 1: Assembly editor dan skrip pengaturan**

`Assets/Eutopos/Editor/Eutopos.Editor.asmdef`:

```json
{
  "name": "Eutopos.Editor",
  "rootNamespace": "Eutopos.EditorTools",
  "references": [],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`Assets/Eutopos/Editor/ProjectSetup.cs`:

```csharp
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eutopos.EditorTools
{
    public static class ProjectSetup
    {
        [MenuItem("Eutopos/Atur Player Android")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "RockHead07";
            PlayerSettings.productName = "eutopos-mobile";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rockhead07.eutopos");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Active Input Handling = Input System (1). Tidak ada API publik untuk ini.
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(asset);
            so.FindProperty("activeInputHandler").intValue = 1;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("eutopos: pengaturan Player Android diterapkan");
        }
    }
}
```

`Assets/Eutopos/Editor/BuildAndroid.cs`:

```csharp
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Eutopos.EditorTools
{
    public static class BuildAndroid
    {
        const string Scene = "Assets/Eutopos/Scenes/Main.unity";

        [MenuItem("Eutopos/Build APK")]
        public static void Build()
        {
            ProjectSetup.ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = "Builds/eutopos-mobile.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("build gagal: " + report.summary.result);
        }
    }
}
```

- [ ] **Step 2: Pengaturan XR dan scene (di Unity Editor, sekali)**

1. Buka proyek di Unity 6000.3.14f1. Jalankan menu **Eutopos > Atur Player Android**. Bila Unity meminta restart karena Input System, setujui.
2. **Edit > Project Settings > XR Plug-in Management > tab Android**: centang **Google ARCore**. Di **XR Plug-in Management > ARCore**: Requirement = **Required**, Depth = **Optional**.
3. **File > New Scene > Basic (Built-in)**, hapus `Main Camera`.
4. **GameObject > XR > AR Session**, lalu **GameObject > XR > XR Origin (Mobile AR)**.
5. Pilih `XR Origin`, **Add Component > AR Anchor Manager**.
6. Buat GameObject kosong bernama `App` (komponennya ditambahkan di Task 8).
7. Simpan sebagai `Assets/Eutopos/Scenes/Main.unity`.

- [ ] **Step 3: Build APK lewat batchmode**

Tutup Editor, lalu:

```powershell
& "D:\Dev\Tools\Unity Editors\6000.3.14f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\Dev\Projects\eutopos-mobile" -buildTarget Android -executeMethod Eutopos.EditorTools.BuildAndroid.Build -logFile "D:\Dev\Projects\eutopos-mobile\Logs\build.log"
echo "exit $LASTEXITCODE"
```

Expected: `exit 0` dan `Builds/eutopos-mobile.apk` ada.

- [ ] **Step 4: Pasang dan buka di ponsel**

```powershell
adb install -r "D:\Dev\Projects\eutopos-mobile\Builds\eutopos-mobile.apk"
adb shell monkey -p com.rockhead07.eutopos -c android.intent.category.LAUNCHER 1
```

Expected: aplikasi meminta izin kamera, lalu menampilkan kamera AR (belum ada UI).

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Editor Assets/Eutopos/Scenes ProjectSettings Packages
git commit -m "chore: Android player settings, AR scene and batch build"
```

### Task 8: Lokalisasi dari ponsel (posisi dan arah)

**Files:**
- Create: `Assets/Eutopos/Scripts/App/Eutopos.App.asmdef`
- Create: `Assets/Eutopos/Scripts/App/Conv.cs`
- Create: `Assets/Eutopos/Scripts/App/VpsConfig.cs`
- Create: `Assets/Eutopos/Scripts/App/VpsClient.cs`
- Create: `Assets/Eutopos/Scripts/App/FrameCapture.cs`
- Create: `Assets/Eutopos/Scripts/App/BuildingRootController.cs`
- Create: `Assets/Eutopos/Scripts/App/SessionLog.cs`
- Create: `Assets/Eutopos/Scripts/App/AppUi.cs`
- Create: `Assets/Eutopos/Scripts/App/AppController.cs`
- Create (lokal, gitignored): `Assets/Eutopos/Resources/vps-token.local.json`

**Interfaces:**
- Consumes: semua kelas inti (Task 2 sampai 6).
- Produces:
  - `Conv.ToUnity(System.Numerics.Vector3) -> UnityEngine.Vector3`, `Conv.ToNum(UnityEngine.Vector3) -> System.Numerics.Vector3`
  - `VpsConfig.Load(out string error) -> VpsConfig` (`BaseUrl`, `ClientId`, `ClientSecret`)
  - `VpsClient.Localize(byte[] jpeg, Intrinsics k, Action<LocalizeResult, float> done) -> IEnumerator`, `VpsClient.FetchNavGraph(Action<NavGraphResult> done) -> IEnumerator`
  - `class CapturedFrame` (`Jpeg`, `K`, `CamPosU`, `CamFwdU` sebagai UnityEngine.Vector3, `TimestampNs`), `FrameCapture.Init(ARCameraManager, Camera)`, `FrameCapture.Request(Action<CapturedFrame, string> done)`
  - `BuildingRootController.Apply(Alignment)`, `CurrentAlignment() -> Alignment?`, `TryUserPoseB(Transform cam, out System.Numerics.Vector3 posB, out float yawB) -> bool`, properti `HasAlignment`
  - `SessionLog.Write(string type, JObject data)`, `SessionLog.FilePath`
  - `AppUi.Build(AppController) -> AppUi` (diperluas di Task 9 dan 10)

- [ ] **Step 1: Assembly aplikasi dan pembantu**

`Assets/Eutopos/Scripts/App/Eutopos.App.asmdef`:

```json
{
  "name": "Eutopos.App",
  "rootNamespace": "Eutopos.App",
  "references": [
    "Eutopos.Core",
    "Unity.XR.ARFoundation",
    "Unity.XR.ARSubsystems",
    "Unity.XR.CoreUtils",
    "Unity.InputSystem",
    "UnityEngine.UI"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["Newtonsoft.Json.dll"],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Bila Unity melaporkan referensi assembly yang tidak ditemukan, buka asmdef di Inspector dan pilih assembly AR Foundation, ARSubsystems, XR Core Utils, Input System, dan uGUI dari daftar (namanya harus sama dengan yang dipakai paket 6.3.4).

`Assets/Eutopos/Scripts/App/Conv.cs`:

```csharp
namespace Eutopos.App
{
    static class Conv
    {
        public static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, v.Z);
        public static System.Numerics.Vector3 ToNum(UnityEngine.Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        public static System.Numerics.Vector2 FloorG(System.Numerics.Vector3 posB) => new System.Numerics.Vector2(posB.X, posB.Z);
    }
}
```

`Assets/Eutopos/Scripts/App/VpsConfig.cs`:

```csharp
using System;
using UnityEngine;

namespace Eutopos.App
{
    public sealed class VpsConfig
    {
        [Serializable]
        sealed class TokenFile { public string base_url; public string client_id; public string client_secret; }

        public string BaseUrl { get; private set; }
        public string ClientId { get; private set; }
        public string ClientSecret { get; private set; }

        /// <summary>Membaca Resources/vps-token.local.json (gitignored). Null dan pesan bila tidak ada.</summary>
        public static VpsConfig Load(out string error)
        {
            var asset = Resources.Load<TextAsset>("vps-token.local");
            if (asset == null)
            {
                error = "vps-token.local.json tidak ada di Assets/Eutopos/Resources";
                return null;
            }
            var f = JsonUtility.FromJson<TokenFile>(asset.text);
            if (f == null || string.IsNullOrEmpty(f.base_url) || string.IsNullOrEmpty(f.client_id) || string.IsNullOrEmpty(f.client_secret))
            {
                error = "vps-token.local.json tidak lengkap (base_url, client_id, client_secret)";
                return null;
            }
            error = null;
            return new VpsConfig { BaseUrl = f.base_url.TrimEnd('/'), ClientId = f.client_id, ClientSecret = f.client_secret };
        }
    }
}
```

Buat berkas lokal (tidak di-commit) dari contoh di akar repo dan isi Client ID dan Secret dari Bagus:

```powershell
New-Item -ItemType Directory -Force "D:\Dev\Projects\eutopos-mobile\Assets\Eutopos\Resources" | Out-Null
Copy-Item "D:\Dev\Projects\eutopos-mobile\vps-token.example.json" "D:\Dev\Projects\eutopos-mobile\Assets\Eutopos\Resources\vps-token.local.json"
git -C "D:\Dev\Projects\eutopos-mobile" check-ignore -v "Assets/Eutopos/Resources/vps-token.local.json"
```

Expected: `check-ignore` mencetak aturan `*.local.json` (berkas diabaikan git).

- [ ] **Step 2: Klien VPS**

`Assets/Eutopos/Scripts/App/VpsClient.cs`:

```csharp
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
```

- [ ] **Step 3: Pengambil frame**

`Assets/Eutopos/Scripts/App/FrameCapture.cs`:

```csharp
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

            var camPos = ArCamera.transform.position;
            var camFwd = ArCamera.transform.forward;
            if (!CameraManager.TryGetIntrinsics(out var intr)) { done(null, "intrinsik kamera tidak tersedia"); return; }
            if (!CameraManager.TryAcquireLatestCpuImage(out var image)) { done(null, "gambar kamera tidak tersedia"); return; }
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
                    done(new CapturedFrame { Jpeg = jpeg, K = k, CamPosU = camPos, CamFwdU = camFwd, TimestampNs = args.timestampNs ?? 0 }, null);
                }
                finally { buffer.Dispose(); }
            }
            finally { image.Dispose(); }
        }
    }
}
```

- [ ] **Step 4: BuildingRoot dan log sesi**

`Assets/Eutopos/Scripts/App/BuildingRootController.cs`:

```csharp
using Eutopos.Core;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.App
{
    /// <summary>BuildingRoot (konten dalam kerangka B) ditempel ARAnchor di pose penyelarasan (spesifikasi bagian 5).</summary>
    public sealed class BuildingRootController : MonoBehaviour
    {
        public ARAnchorManager AnchorManager;
        public Transform BuildingRoot;
        public bool HasAlignment { get; private set; }

        ARAnchor anchor;

        public async void Apply(Alignment a)
        {
            var pose = new Pose(Conv.ToUnity(a.Translation), Quaternion.Euler(0f, a.YawDeg, 0f));
            HasAlignment = true;
            // Langsung tempatkan supaya tampilan tidak menunggu anchor; anchor menyusul.
            BuildingRoot.SetParent(null, false);
            BuildingRoot.SetPositionAndRotation(pose.position, pose.rotation);
            var result = await AnchorManager.TryAddAnchorAsync(pose);
            if (!result.status.IsSuccess()) return;
            var old = anchor;
            anchor = result.value;
            BuildingRoot.SetParent(anchor.transform, false);
            BuildingRoot.localPosition = Vector3.zero;
            BuildingRoot.localRotation = Quaternion.identity;
            if (old != null) AnchorManager.TryRemoveAnchor(old);
        }

        /// <summary>Penyelarasan saat ini dibaca dari transform BuildingRoot (ikut pembaruan anchor).</summary>
        public Alignment? CurrentAlignment()
        {
            if (!HasAlignment) return null;
            return new Alignment(BuildingRoot.rotation.eulerAngles.y, Conv.ToNum(BuildingRoot.position));
        }

        public bool TryUserPoseB(Transform cam, out System.Numerics.Vector3 posB, out float yawB)
        {
            posB = default;
            yawB = float.NaN;
            if (!HasAlignment) return false;
            posB = Conv.ToNum(BuildingRoot.InverseTransformPoint(cam.position));
            var yaw = FrameConvert.YawDeg(Conv.ToNum(BuildingRoot.InverseTransformDirection(cam.forward)));
            if (yaw != null) yawB = yaw.Value;
            return true;
        }
    }
}
```

Catatan verifikasi: nama `TryAddAnchorAsync`, `result.status.IsSuccess()`, `result.value`, dan `TryRemoveAnchor` mengikuti AR Foundation 6.x. Bila kompilasi menolaknya, cocokkan dengan dokumentasi `ARAnchorManager` versi 6.3 yang terpasang (Package Manager > AR Foundation > Documentation) dan ganti hanya pemanggilan itu.

`Assets/Eutopos/Scripts/App/SessionLog.cs`:

```csharp
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>Satu JSONL per sesi di persistentDataPath/logs (spesifikasi bagian 11).</summary>
    public sealed class SessionLog : IDisposable
    {
        readonly StreamWriter writer;
        readonly float t0;
        public string FilePath { get; }

        public SessionLog(string dir)
        {
            var logs = Path.Combine(dir, "logs");
            Directory.CreateDirectory(logs);
            FilePath = Path.Combine(logs, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
            t0 = Time.realtimeSinceStartup;
        }

        public void Write(string type, JObject data)
        {
            data["t"] = Math.Round(Time.realtimeSinceStartup - t0, 3);
            data["type"] = type;
            writer.WriteLine(data.ToString(Formatting.None));
        }

        public void Dispose() => writer.Dispose();
    }
}
```

- [ ] **Step 5: UI dasar dan pengendali**

`Assets/Eutopos/Scripts/App/AppUi.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>UI dibangun dari kode supaya scene tetap minimal.</summary>
    public sealed class AppUi
    {
        public Text Logo, Status, Banner, TargetLabel;
        public Button LocalizeButton, NavButton, PrevTarget, NextTarget;
        public RawImage MiniMap;
        public GameObject DebugRoot;
        public Text DebugInterval, DebugJump, DebugPoint, DebugInfo;

        static Font font;
        static Font F => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static AppUi Build(AppController app)
        {
            var ui = new AppUi();
            var canvas = NewCanvas();

            var top = Panel(canvas, new Vector2(0, 0.9f), new Vector2(1, 1), new Color(0, 0, 0, 0.55f));
            ui.Logo = Label(top, "eutopos", 44, TextAnchor.MiddleLeft, new Vector2(0.03f, 0), new Vector2(0.3f, 1));
            var logoButton = ui.Logo.gameObject.AddComponent<Button>();
            logoButton.onClick.AddListener(app.OnLogoTapped);
            ui.Status = Label(top, "", 28, TextAnchor.MiddleLeft, new Vector2(0.3f, 0), new Vector2(0.98f, 1));

            ui.MiniMap = new GameObject("MiniMap", typeof(RawImage)).GetComponent<RawImage>();
            Place(ui.MiniMap.rectTransform, canvas, new Vector2(0.6f, 0.62f), new Vector2(0.98f, 0.88f));

            var bannerPanel = Panel(canvas, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.56f), new Color(0.1f, 0.4f, 0.2f, 0.85f));
            ui.Banner = Label(bannerPanel, "", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            bannerPanel.gameObject.SetActive(false);

            var bottom = Panel(canvas, new Vector2(0, 0), new Vector2(1, 0.12f), new Color(0, 0, 0, 0.55f));
            var row = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 16, 16);
            row.spacing = 12;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            ui.LocalizeButton = NewButton(bottom, "Lokalisasi", app.OnLocalizePressed);
            ui.PrevTarget = NewButton(bottom, "<", () => app.CycleTarget(-1));
            ui.TargetLabel = Label(bottom, "Target: -", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            ui.NextTarget = NewButton(bottom, ">", () => app.CycleTarget(1));
            ui.NavButton = NewButton(bottom, "Mulai", app.OnNavPressed);

            ui.DebugRoot = DebugPanel.Build(ui, canvas, app); // Task 10; sebelum itu kembalikan null
            return ui;
        }

        public void ShowBanner(string text)
        {
            Banner.text = text;
            Banner.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        static RectTransform NewCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return (RectTransform)go.transform;
        }

        internal static void Place(RectTransform rt, Transform parent, Vector2 min, Vector2 max)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        internal static RectTransform Panel(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.GetComponent<Image>().color = color;
            var rt = (RectTransform)go.transform;
            Place(rt, parent, min, max);
            return rt;
        }

        internal static Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 min, Vector2 max)
        {
            var t = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            Place(t.rectTransform, parent, min, max);
            t.font = F;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = text;
            return t;
        }

        internal static Button NewButton(Transform parent, string text, Action onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.85f, 0.95f);
            Place((RectTransform)go.transform, parent, Vector2.zero, Vector2.one);
            Label(go.transform, text, 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onClick());
            return b;
        }
    }
}
```

Sampai Task 10, buat kelas sementara `Assets/Eutopos/Scripts/App/DebugPanel.cs` agar kode di atas terkompilasi:

```csharp
using UnityEngine;

namespace Eutopos.App
{
    public static class DebugPanel
    {
        public static GameObject Build(AppUi ui, Transform canvas, AppController app) => null;
    }
}
```

`Assets/Eutopos/Scripts/App/AppController.cs` (versi Task 8: lokalisasi, status, log; rute ditambah di Task 9):

```csharp
using System.IO;
using Eutopos.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.App
{
    public sealed partial class AppController : MonoBehaviour
    {
        ARCameraManager cameraManager;
        Camera arCamera;
        FrameCapture capture;
        VpsClient vps;
        BuildingRootController rootCtl;
        SessionLog log;
        AppUi ui;

        readonly CorrectionScheduler scheduler = new CorrectionScheduler();
        readonly JumpGuard guard = new JumpGuard();
        int consecutiveFailures, acceptedCount;
        bool needsRelocalize = true, wasTracking;
        float lastPoseLogT;
        string configError, lastMessage = "Tekan Lokalisasi";
        LocalizeResult lastResult;
        float lastCorrectionT = float.NegativeInfinity;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            cameraManager = FindFirstObjectByType<ARCameraManager>();
            arCamera = cameraManager.GetComponent<Camera>();
            var root = new GameObject("BuildingRoot").transform;

            capture = gameObject.AddComponent<FrameCapture>();
            capture.Init(cameraManager, arCamera);
            vps = gameObject.AddComponent<VpsClient>();
            vps.Config = VpsConfig.Load(out configError);
            rootCtl = gameObject.AddComponent<BuildingRootController>();
            rootCtl.AnchorManager = FindFirstObjectByType<ARAnchorManager>();
            rootCtl.BuildingRoot = root;
            log = new SessionLog(Application.persistentDataPath);
            ui = AppUi.Build(this);
            AwakeNavigation(root); // Task 9
        }

        void Start()
        {
            log.Write("session_start", new JObject
            {
                ["device"] = SystemInfo.deviceModel,
                ["app_version"] = Application.version,
                ["interval_s"] = scheduler.IntervalS,
                ["jump_m"] = guard.JumpThresholdM,
                ["config_error"] = configError,
            });
            if (vps.Config != null) StartCoroutine(vps.FetchNavGraph(OnNavGraph)); // OnNavGraph: Task 9
        }

        void OnDestroy() => log?.Dispose();

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (tracking != wasTracking)
            {
                log.Write("tracking", new JObject { ["tracking"] = tracking, ["state"] = ARSession.state.ToString() });
                if (!tracking) { needsRelocalize = true; lastMessage = "Gerakkan ponsel perlahan"; }
                wasTracking = tracking;
            }
            scheduler.Tracking = tracking;
            if (scheduler.ShouldFireTimer(now)) StartCorrection("timer");
            if (now - lastPoseLogT >= 1f) { lastPoseLogT = now; LogPose(); }
            UpdateNavigation(now); // Task 9
            UpdateStatus(now);
        }

        public void OnLocalizePressed() => StartCorrection("button");

        void StartCorrection(string trigger)
        {
            if (vps.Config == null) { lastMessage = configError; return; }
            if (!scheduler.TryStart(Time.realtimeSinceStartup)) return;
            capture.Request((frame, error) => OnCaptured(frame, error, trigger));
        }

        void OnCaptured(CapturedFrame frame, string error, string trigger)
        {
            if (frame == null)
            {
                scheduler.Finish();
                lastMessage = error;
                log.Write("correction", new JObject { ["trigger"] = trigger, ["decision"] = "rejected", ["reason"] = error });
                return;
            }
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "last-frame.jpg"), frame.Jpeg);
            StartCoroutine(vps.Localize(frame.Jpeg, frame.K, (r, latency) => OnLocalized(frame, r, latency, trigger)));
        }

        void OnLocalized(CapturedFrame frame, LocalizeResult r, float latency, string trigger)
        {
            scheduler.Finish();
            lastResult = r;
            lastCorrectionT = Time.realtimeSinceStartup;
            var entry = new JObject
            {
                ["trigger"] = trigger,
                ["status"] = r.Status.ToString(),
                ["inliers"] = r.Inliers,
                ["latency_s"] = latency,
                ["server_t_s"] = JObject.FromObject(r.TimingsS),
                ["map_version"] = r.MapVersion,
                ["k"] = new JArray(frame.K.Fx, frame.K.Fy, frame.K.Cx, frame.K.Cy, frame.K.Width, frame.K.Height),
            };
            switch (r.Status)
            {
                case VpsStatus.Ok:
                    HandleOk(frame, r, entry);
                    break;
                case VpsStatus.Failed:
                    consecutiveFailures++;
                    lastMessage = consecutiveFailures >= 3
                        ? "Arahkan kamera ke area bertekstur, bukan dinding polos"
                        : $"Koreksi gagal ({r.Inliers} inlier)";
                    entry["decision"] = "rejected";
                    entry["reason"] = "few_inliers";
                    break;
                case VpsStatus.NotAligned:
                    lastMessage = "Peta belum diselaraskan";
                    entry["decision"] = "rejected";
                    entry["reason"] = "not_aligned";
                    break;
                case VpsStatus.NoMap:
                    lastMessage = "Layanan belum punya peta";
                    entry["decision"] = "rejected";
                    entry["reason"] = "no_map";
                    break;
                case VpsStatus.Unauthorized:
                    scheduler.Navigating = false;
                    lastMessage = "Token akses tidak ada atau salah";
                    entry["decision"] = "rejected";
                    entry["reason"] = "unauthorized";
                    break;
                case VpsStatus.NetworkError:
                    lastMessage = "Koreksi gagal (jaringan)";
                    entry["decision"] = "rejected";
                    entry["reason"] = "network";
                    break;
                default:
                    lastMessage = "Galat server: " + r.Detail;
                    entry["decision"] = "rejected";
                    entry["reason"] = "server_error";
                    break;
            }
            log.Write("correction", entry);
        }

        void HandleOk(CapturedFrame frame, LocalizeResult r, JObject entry)
        {
            var posB = FrameConvert.GToB(r.PositionG);
            var fwdB = FrameConvert.ForwardB(r.RotationG);
            var camPosU = Conv.ToNum(frame.CamPosU);
            entry["pose_b"] = new JArray(posB.X, posB.Y, posB.Z);
            var proposed = Aligner4Dof.Solve(camPosU, Conv.ToNum(frame.CamFwdU), posB, fwdB);
            if (proposed == null)
            {
                lastMessage = "Tegakkan ponsel";
                entry["decision"] = "rejected";
                entry["reason"] = "camera_too_steep";
                return;
            }
            var g = guard.Evaluate(Time.realtimeSinceStartup, rootCtl.CurrentAlignment(), proposed.Value, camPosU);
            entry["jump_m"] = g.JumpM;
            entry["dropped_candidate"] = g.DroppedCandidate;
            entry["decision"] = g.Decision.ToString().ToLowerInvariant();
            if (g.Decision == CorrectionDecision.Candidate)
            {
                lastMessage = $"Lompatan {g.JumpM:0.0} m ditahan, menunggu konfirmasi";
                return;
            }
            rootCtl.Apply(proposed.Value);
            consecutiveFailures = 0;
            acceptedCount++;
            needsRelocalize = false;
            lastMessage = $"Posisi diperbarui ({r.Inliers} inlier)";
            OnCorrectionAccepted(); // Task 9
        }

        void LogPose()
        {
            var t = arCamera.transform;
            var e = new JObject
            {
                ["u_pos"] = new JArray(t.position.x, t.position.y, t.position.z),
                ["u_rot"] = new JArray(t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w),
            };
            if (rootCtl.TryUserPoseB(t, out var posB, out var yawB))
            {
                e["b_pos"] = new JArray(posB.X, posB.Y, posB.Z);
                e["b_yaw"] = float.IsNaN(yawB) ? null : (JToken)yawB;
            }
            log.Write("pose", e);
        }

        void UpdateStatus(float now)
        {
            string age = float.IsNegativeInfinity(lastCorrectionT) ? "-" : $"{now - lastCorrectionT:0}s";
            string inl = lastResult == null ? "-" : lastResult.Inliers.ToString();
            string ver = lastResult?.MapVersion?.ToString() ?? "-";
            string track = wasTracking ? "lacak" : "hilang";
            ui.Status.text = $"peta v{ver} | {inl} inlier | {age} | {track}\n{lastMessage}";
        }
    }
}
```

Sementara Task 9 belum dikerjakan, tambahkan `Assets/Eutopos/Scripts/App/AppController.Navigation.cs` minimal agar terkompilasi:

```csharp
using Eutopos.Core;
using UnityEngine;

namespace Eutopos.App
{
    public sealed partial class AppController
    {
        void AwakeNavigation(Transform root) { }
        void OnNavGraph(NavGraphResult r) { }
        void UpdateNavigation(float now) { }
        void OnCorrectionAccepted() { }
        public void CycleTarget(int step) { }
        public void OnNavPressed() { }
        public void OnLogoTapped() { }
    }
}
```

Di scene `Main.unity`, tambahkan komponen **AppController** pada objek `App`, lalu simpan.

- [ ] **Step 6: Build, pasang, dan periksa bagian 14 spesifikasi di ponsel**

Build dan pasang seperti Task 7 Step 3 dan 4. Lalu, di lorong lantai 10:

1. Tekan **Lokalisasi**. Expected: bilah status menampilkan inlier dan "Posisi diperbarui", atau "Peta belum diselaraskan" bila `align.json` belum dipasang (itu tetap bukti bahwa `/localize` menjawab `ok`).
2. **Foto tegak:** `adb pull /sdcard/Android/data/com.rockhead07.eutopos/files/last-frame.jpg` lalu buka. Expected: foto tegak, tidak miring 90 derajat, tidak terbalik. Bila miring, ganti arah putar (CW menjadi CCW) di `FrameCapture` dan `Intrinsics`, tambahkan tesnya di Task 2, lalu ulangi.
3. **Resolusi intrinsik:** di log, kejadian `correction` berisi `k` (fx, fy, cx, cy, lebar, tinggi). Bandingkan lebar dan tinggi dengan ukuran `last-frame.jpg`. Expected: sama.
4. **Gravitasi:** letakkan ponsel diam tegak lalu putar pelan; di log `pose`, komponen Y dari arah atas kamera (`u_rot`) tetap searah sumbu +Y sesi. Bila tidak, catat dan bahas pilihan 6-DoF (spesifikasi bagian 14) dengan Bagus sebelum lanjut.
5. **Latensi:** ambil median `latency_s` dari 10 koreksi. Bila jauh di atas 5 detik, naikkan interval awal di Task 10 dan catat.
6. **Token tidak membuka dashboard** (di laptop, bukan ponsel):

```powershell
curl.exe -s -o NUL -w "%{http_code}`n" -H "CF-Access-Client-Id: <ID>" -H "CF-Access-Client-Secret: <SECRET>" https://eutopos.rockhead07.tech/health
curl.exe -s -o NUL -w "%{http_code}`n" -H "CF-Access-Client-Id: <ID>" -H "CF-Access-Client-Secret: <SECRET>" https://eutopos.rockhead07.tech/api/jobs
```

Expected: `/health` 200, `/api/jobs` 401 atau 403. Jangan menempel nilai token ke chat atau berkas yang di-commit.

Ambil log untuk dicatat: `adb pull /sdcard/Android/data/com.rockhead07.eutopos/files/logs device-logs` (folder `device-logs/` gitignored).

- [ ] **Step 7: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/App Assets/Eutopos/Scenes/Main.unity
git commit -m "feat(app): localize from ARCore frames, align the building root and log sessions"
```

### Task 9: Rute, Sampai, dan denah mini

**Files:**
- Create: `Assets/Eutopos/Scripts/App/RouteView.cs`
- Create: `Assets/Eutopos/Scripts/App/MiniMap.cs`
- Modify: `Assets/Eutopos/Scripts/App/AppController.Navigation.cs` (ganti versi kosong)

**Interfaces:**
- Consumes: `NavGraph`, `Route`, `NavGraph.Arrived`, `BuildingRootController.TryUserPoseB`, `AppUi`, `SessionLog`.
- Produces: `RouteView.Show(Route)`, `RouteView.Clear()`; `MiniMap.Draw(NavGraph, Route, Vector2? userG, float yawB)`; implementasi penuh `AppController` bagian navigasi.

- [ ] **Step 1: Tampilan rute AR**

`Assets/Eutopos/Scripts/App/RouteView.cs`:

```csharp
using System.Collections.Generic;
using Eutopos.Core;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>Garis rute di lantai (Y_B = 0,02) dan panah ke arah 1,5 m di depan, dalam koordinat lokal BuildingRoot (B).</summary>
    public sealed class RouteView : MonoBehaviour
    {
        const float FloorY = 0.02f, ArrowAheadM = 1.5f, ArrowSizeM = 0.4f;
        LineRenderer line, arrow;

        void Awake()
        {
            line = NewLine("RouteLine", 0.15f);
            arrow = NewLine("RouteArrow", 0.12f);
        }

        LineRenderer NewLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.18f, 0.44f, 0.87f, 0.9f);
            lr.positionCount = 0;
            return lr;
        }

        static Vector3 OnFloor(System.Numerics.Vector2 g) => new Vector3(g.X, FloorY, g.Y);

        public void Show(Route route)
        {
            var pts = new List<Vector3>();
            foreach (var p in route.Points) pts.Add(OnFloor(p));
            line.positionCount = pts.Count;
            line.SetPositions(pts.ToArray());

            // titik dan arah pada jarak ArrowAheadM sepanjang rute
            float left = ArrowAheadM;
            for (int i = 1; i < pts.Count; i++)
            {
                var seg = pts[i] - pts[i - 1];
                float len = seg.magnitude;
                if (len < 1e-4f) continue;
                if (left <= len || i == pts.Count - 1)
                {
                    var dir = seg / len;
                    var tip = pts[i - 1] + dir * Mathf.Min(left, len);
                    var side = Vector3.Cross(Vector3.up, dir) * ArrowSizeM;
                    arrow.positionCount = 3;
                    arrow.SetPositions(new[] { tip - dir * ArrowSizeM + side, tip, tip - dir * ArrowSizeM - side });
                    return;
                }
                left -= len;
            }
            arrow.positionCount = 0;
        }

        public void Clear()
        {
            line.positionCount = 0;
            arrow.positionCount = 0;
        }
    }
}
```

Bila `Shader.Find("Sprites/Default")` mengembalikan null di APK, tambahkan shader itu ke **Project Settings > Graphics > Always Included Shaders**.

- [ ] **Step 2: Denah mini**

`Assets/Eutopos/Scripts/App/MiniMap.cs`:

```csharp
using System;
using Eutopos.Core;
using UnityEngine;
using UnityEngine.UI;
using NumV2 = System.Numerics.Vector2;

namespace Eutopos.App
{
    /// <summary>Denah 2D di kerangka G: sisi abu, rute biru, pengguna merah dengan garis arah.</summary>
    public sealed class MiniMap
    {
        const int Size = 256, Margin = 16;
        readonly Texture2D tex;
        readonly Color32[] clear;

        public MiniMap(RawImage target)
        {
            tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            clear = new Color32[Size * Size];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(0, 0, 0, 140);
            target.texture = tex;
        }

        public void Draw(NavGraph graph, Route route, NumV2? userG, float yawB)
        {
            tex.SetPixels32(clear);
            if (graph != null)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var p in graph.Nodes.Values)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
                float span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1f);
                float scale = (Size - 2 * Margin) / span;
                Vector2Int Px(NumV2 p) => new Vector2Int(Margin + (int)((p.X - minX) * scale), Margin + (int)((p.Y - minY) * scale));

                foreach (var a in graph.Nodes)
                    foreach (var b in graph.Nodes)
                        if (string.CompareOrdinal(a.Key, b.Key) < 0 && Connected(graph, a.Key, b.Key))
                            Line(Px(a.Value), Px(b.Value), new Color32(160, 160, 160, 255));
                if (route != null)
                    for (int i = 1; i < route.Points.Count; i++)
                        Line(Px(route.Points[i - 1]), Px(route.Points[i]), new Color32(60, 120, 230, 255));
                if (userG.HasValue)
                {
                    var u = Px(userG.Value);
                    Dot(u, 4, new Color32(230, 60, 60, 255));
                    if (!float.IsNaN(yawB))
                    {
                        // yaw B dari +Z_B (= +y_G) ke +X_B (= +x_G)
                        float r = yawB * Mathf.Deg2Rad;
                        var head = new Vector2Int(u.x + (int)(Mathf.Sin(r) * 18), u.y + (int)(Mathf.Cos(r) * 18));
                        Line(u, head, new Color32(230, 60, 60, 255));
                    }
                }
            }
            tex.Apply();
        }

        static bool Connected(NavGraph g, string a, string b) => g.HasEdge(a, b);

        void Line(Vector2Int a, Vector2Int b, Color32 c)
        {
            int dx = Math.Abs(b.x - a.x), dy = -Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, err = dx + dy;
            int x = a.x, y = a.y;
            while (true)
            {
                Set(x, y, c);
                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        void Dot(Vector2Int c, int r, Color32 col)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Set(c.x + x, c.y + y, col);
        }

        void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size) tex.SetPixel(x, y, c);
        }
    }
}
```

Tambahkan ke `NavGraph` (Core, Task 5) satu metode yang dipakai denah, beserta tesnya di `NavGraphTests`:

```csharp
        public bool HasEdge(string a, string b) => adj.TryGetValue(a, out var n) && n.Contains(b);
```

```csharp
        [Test]
        public void HasEdgeIsSymmetric()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.HasEdge("P01", "P02") && g.HasEdge("P02", "P01"), Is.True);
            Assert.That(g.HasEdge("P01", "P03"), Is.False);
        }
```

Jalankan tes EditMode (Task 1 Step 6) setelah menambah metode ini. Expected: `exit 0`.

- [ ] **Step 3: Navigasi di AppController**

Ganti seluruh isi `Assets/Eutopos/Scripts/App/AppController.Navigation.cs`:

```csharp
using Eutopos.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NumV2 = System.Numerics.Vector2;

namespace Eutopos.App
{
    public sealed partial class AppController
    {
        RouteView routeView;
        MiniMap miniMap;
        NavGraph graph;
        Route route;
        int targetIndex = -1;
        bool navigating;
        NumV2 lastPlanPos;
        float navStartT, lastMiniMapT;
        int acceptedAtStart;

        void AwakeNavigation(Transform root)
        {
            routeView = root.gameObject.AddComponent<RouteView>();
            miniMap = new MiniMap(ui.MiniMap);
            RefreshTargetLabel();
        }

        void OnNavGraph(NavGraphResult r)
        {
            log.Write("navgraph", new JObject { ["status"] = r.Status.ToString(), ["detail"] = r.Detail, ["map_version"] = r.MapVersion });
            if (r.Status == VpsStatus.Ok)
            {
                graph = r.Graph;
                targetIndex = graph.Targets.Count > 0 ? 0 : -1;
            }
            else if (r.Status == VpsStatus.NotFound) lastMessage = "Graf rute belum dipasang; target nonaktif";
            else if (r.Status == VpsStatus.Unauthorized) lastMessage = "Token akses tidak ada atau salah";
            else lastMessage = "Graf rute gagal dimuat: " + r.Detail;
            RefreshTargetLabel();
        }

        public void CycleTarget(int step)
        {
            if (graph == null || graph.Targets.Count == 0 || navigating) return;
            targetIndex = (targetIndex + step + graph.Targets.Count) % graph.Targets.Count;
            RefreshTargetLabel();
        }

        void RefreshTargetLabel()
        {
            bool has = graph != null && targetIndex >= 0;
            ui.TargetLabel.text = has ? "Target: " + graph.Targets[targetIndex].Name : "Target: -";
            ui.PrevTarget.interactable = ui.NextTarget.interactable = ui.NavButton.interactable = has;
        }

        public void OnNavPressed()
        {
            if (navigating) { StopNavigation("berhenti"); return; }
            if (graph == null || targetIndex < 0) return;
            if (!rootCtl.HasAlignment || needsRelocalize) { lastMessage = "Tekan Lokalisasi dulu"; return; }
            navigating = true;
            scheduler.Navigating = true;
            navStartT = Time.realtimeSinceStartup;
            acceptedAtStart = acceptedCount;
            ui.NavButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Berhenti";
            ui.ShowBanner(null);
            Replan();
        }

        void StopNavigation(string reason)
        {
            navigating = false;
            scheduler.Navigating = false;
            routeView.Clear();
            route = null;
            ui.NavButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Mulai";
            log.Write("nav_stop", new JObject { ["reason"] = reason });
        }

        void OnCorrectionAccepted()
        {
            if (navigating) Replan();
        }

        void Replan()
        {
            if (!rootCtl.TryUserPoseB(arCamera.transform, out var posB, out _)) return;
            var user = Conv.FloorG(posB);
            var target = graph.Targets[targetIndex];
            route = graph.Plan(user, target.Node);
            lastPlanPos = user;
            if (route == null)
            {
                routeView.Clear();
                lastMessage = "Target tidak terjangkau dari posisi ini";
                log.Write("route", new JObject { ["target"] = target.Node, ["reachable"] = false });
                return;
            }
            routeView.Show(route);
            log.Write("route", new JObject
            {
                ["target"] = target.Node,
                ["reachable"] = true,
                ["nodes"] = new JArray(route.NodeIds),
                ["length_m"] = route.LengthM,
            });
        }

        void UpdateNavigation(float now)
        {
            bool hasPose = rootCtl.TryUserPoseB(arCamera.transform, out var posB, out var yawB);
            NumV2? user = hasPose ? Conv.FloorG(posB) : (NumV2?)null;
            if (navigating && user.HasValue && !needsRelocalize)
            {
                if (NumV2.Distance(user.Value, lastPlanPos) > 1f) Replan();
                var target = graph.Targets[targetIndex];
                var goal = graph.Nodes[target.Node];
                if (NavGraph.Arrived(user.Value, goal))
                {
                    float d = NumV2.Distance(user.Value, goal);
                    ui.ShowBanner($"Sampai di {target.Name} ({d:0.00} m menurut aplikasi)");
                    log.Write("arrival", new JObject
                    {
                        ["target"] = target.Node,
                        ["app_distance_m"] = d,
                        ["duration_s"] = now - navStartT,
                        ["corrections_accepted"] = acceptedCount - acceptedAtStart,
                    });
                    StopNavigation("arrived");
                }
            }
            if (navigating && needsRelocalize) routeView.Clear();
            if (now - lastMiniMapT >= 0.2f)
            {
                lastMiniMapT = now;
                miniMap.Draw(graph, route, user, yawB);
            }
        }

        public void OnLogoTapped() => DebugPanel.OnLogoTapped(this); // Task 10
    }
}
```

Tambahkan sementara di `DebugPanel.cs` (diganti di Task 10):

```csharp
        public static void OnLogoTapped(AppController app) { }
```

- [ ] **Step 4: Build dan uji navigasi di ponsel**

Prasyarat: `navgraph.json` sudah dipasang di server (`manage align --navgraph`). Build dan pasang seperti Task 7.

1. Lokalisasi di dekat P1. Expected: denah mini menampilkan graf, titik merah, dan garis arah yang sejalan dengan arah ponsel.
2. Pilih target, tekan **Mulai**. Expected: garis biru di lantai dan panah ke arah belokan; tombol berubah menjadi "Berhenti".
3. Jalan ke target. Expected: koreksi otomatis tiap 5 detik di log (`trigger: timer`); banner Sampai muncul dalam radius 1 m.
4. Pilih target yang tidak terhubung (bila ada). Expected: pesan "Target tidak terjangkau dari posisi ini", aplikasi tidak berhenti.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts
git commit -m "feat(app): route on the floor, arrival detection and mini map"
```

### Task 10: Panel uji tersembunyi (ground truth dan pengaturan)

**Files:**
- Modify: `Assets/Eutopos/Scripts/App/DebugPanel.cs` (ganti versi sementara)
- Modify: `Assets/Eutopos/Scripts/App/AppController.Navigation.cs` (metode publik untuk panel)

**Interfaces:**
- Consumes: `AppUi.Panel`, `AppUi.Label`, `AppUi.NewButton`, `AppUi.Place`, `CorrectionScheduler.IntervalS`, `JumpGuard.JumpThresholdM`, `NavGraph.Nodes`, `SessionLog`.
- Produces: `DebugPanel.Build(AppUi, Transform, AppController) -> GameObject`, `DebugPanel.OnLogoTapped(AppController)`, dan di `AppController`: `AdjustInterval(float deltaS)`, `AdjustJump(float deltaM)`, `CyclePoint(int step)`, `MarkGroundTruth()`, `ToggleDebug()`.

- [ ] **Step 1: Panel uji**

`Assets/Eutopos/Scripts/App/DebugPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>Panel tersembunyi: ketuk logo 5 kali dalam 3 detik (pola ADR-025 di DARSI).</summary>
    public static class DebugPanel
    {
        static int taps;
        static float firstTapT;

        public static void OnLogoTapped(AppController app)
        {
            float now = Time.realtimeSinceStartup;
            if (now - firstTapT > 3f) { taps = 0; firstTapT = now; }
            if (++taps >= 5) { taps = 0; app.ToggleDebug(); }
        }

        public static GameObject Build(AppUi ui, Transform canvas, AppController app)
        {
            var panel = AppUi.Panel(canvas, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.6f), new Color(0, 0, 0, 0.85f));
            var col = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(20, 20, 20, 20);
            col.spacing = 14;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = true;

            ui.DebugInterval = Row(panel, "Interval -", () => app.AdjustInterval(-1f), "+", () => app.AdjustInterval(1f));
            ui.DebugJump = Row(panel, "Lompatan -", () => app.AdjustJump(-0.5f), "+", () => app.AdjustJump(0.5f));
            ui.DebugPoint = Row(panel, "<", () => app.CyclePoint(-1), ">", () => app.CyclePoint(1));
            AppUi.NewButton(panel, "Saya berdiri di titik ini", app.MarkGroundTruth);
            ui.DebugInfo = AppUi.Label(panel, "", 24, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static Text Row(RectTransform parent, string left, System.Action onLeft, string right, System.Action onRight)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            AppUi.Place((RectTransform)row.transform, parent, Vector2.zero, Vector2.one);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            AppUi.NewButton(row.transform, left, onLeft);
            var label = AppUi.Label(row.transform, "", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AppUi.NewButton(row.transform, right, onRight);
            return label;
        }
    }
}
```

- [ ] **Step 2: Metode panel di AppController**

Tambahkan ke `AppController.Navigation.cs` (di dalam kelas):

```csharp
        int pointIndex;

        public void ToggleDebug()
        {
            if (ui.DebugRoot == null) return;
            ui.DebugRoot.SetActive(!ui.DebugRoot.activeSelf);
            RefreshDebug();
        }

        public void AdjustInterval(float deltaS)
        {
            scheduler.IntervalS = Mathf.Clamp(scheduler.IntervalS + deltaS, 2f, 30f);
            RefreshDebug();
        }

        public void AdjustJump(float deltaM)
        {
            guard.JumpThresholdM = Mathf.Clamp(guard.JumpThresholdM + deltaM, 1f, 10f);
            RefreshDebug();
        }

        public void CyclePoint(int step)
        {
            if (graph == null) return;
            pointIndex = (pointIndex + step + graph.Nodes.Count) % graph.Nodes.Count;
            RefreshDebug();
        }

        string SelectedPoint()
        {
            if (graph == null) return null;
            int i = 0;
            foreach (var id in graph.Nodes.Keys) { if (i++ == pointIndex) return id; }
            return null;
        }

        public void MarkGroundTruth()
        {
            var id = SelectedPoint();
            if (id == null || !rootCtl.TryUserPoseB(arCamera.transform, out var posB, out var yawB))
            {
                ui.DebugInfo.text = "Belum ada graf atau belum ada posisi";
                return;
            }
            var app = Conv.FloorG(posB);
            var truth = graph.Nodes[id];
            float err = NumV2.Distance(app, truth);
            log.Write("ground_truth", new JObject
            {
                ["point"] = id,
                ["app_xy"] = new JArray(app.X, app.Y),
                ["truth_xy"] = new JArray(truth.X, truth.Y),
                ["error_m"] = err,
                ["app_yaw_b"] = float.IsNaN(yawB) ? null : (JToken)yawB,
            });
            ui.DebugInfo.text = $"{id}: galat {err:0.00} m dicatat\n{log.FilePath}";
        }

        void RefreshDebug()
        {
            if (ui.DebugRoot == null || !ui.DebugRoot.activeSelf) return;
            ui.DebugInterval.text = $"{scheduler.IntervalS:0} s";
            ui.DebugJump.text = $"{guard.JumpThresholdM:0.0} m";
            ui.DebugPoint.text = SelectedPoint() ?? "-";
            ui.DebugInfo.text = log.FilePath;
        }
```

Step 1 sudah mengganti seluruh `DebugPanel.cs`, termasuk `OnLogoTapped` yang sebenarnya. `AppController.OnLogoTapped` (Task 9) tetap memanggil `DebugPanel.OnLogoTapped(this)`.

- [ ] **Step 3: Build dan uji panel**

Build dan pasang seperti Task 7. Ketuk logo 5 kali. Expected: panel muncul; interval dan ambang lompatan berubah dan tercatat di status; berdiri di titik terukur, pilih ID-nya, tekan "Saya berdiri di titik ini". Expected: log berisi `ground_truth` dengan `error_m`.

- [ ] **Step 4: Jalankan tes EditMode lagi** (regresi inti)

Run: perintah uji batchmode di Task 1 Step 6.
Expected: `exit 0`.

- [ ] **Step 5: Commit (titik commit yang diusulkan)**

```bash
git add Assets/Eutopos/Scripts/App
git commit -m "feat(app): hidden test panel for ground truth and correction settings"
```

### Task 11: Uji lapangan T1 sampai T3

**Files:**
- Create: `docs/field/2026-10-mvp-uji-lapangan.md` (hanya prosedur dan tabel kosong; hasil dan koordinat TIDAK di-commit)

**Interfaces:**
- Consumes: aplikasi dari Task 10; `align.json` dan `navgraph.json` terpasang (`eutopos-vps`, `manage align`); titik meteran dari lembar lapangan.

- [ ] **Step 1: Tulis prosedur**

`docs/field/2026-10-mvp-uji-lapangan.md`:

```markdown
# Prosedur uji lapangan MVP (spesifikasi bagian 12)

Hasil mentah (log JSONL, koordinat, foto) disimpan di luar git.

## Persiapan
- `align.json` dan `navgraph.json` terpasang untuk versi peta aktif; `/health` menunjukkan `aligned: true`.
- Ponsel perekam peta, baterai penuh, aplikasi versi terbaru.
- Titik untuk menyetel parameter (interval, ambang) berbeda dengan titik untuk melaporkan hasil.

## T1 Lokalisasi di titik (per titik, minimal 3 kali)
1. Berdiri dengan ujung kaki di titik terukur, ponsel setinggi dada.
2. Tekan Lokalisasi, tunggu "Posisi diperbarui".
3. Panel uji: pilih ID titik, tekan "Saya berdiri di titik ini".

## T2 Navigasi P1 ke P2 (per pasangan, minimal 3 kali)
1. Lokalisasi di P1. Pilih target P2, tekan Mulai.
2. Ikuti garis; jangan menunjuk dinding polos lama-lama.
3. Saat banner Sampai muncul, berhenti di tempat dan ukur jarak sebenarnya ke P2 dengan meteran.
4. Berhasil bila Sampai muncul DAN jarak sebenarnya ≤ 1,0 m.

## T3 Kehilangan pelacakan
1. Di tengah rute, tutup kamera 3 sampai 5 detik.
2. Catat: pesan muncul, rute disembunyikan, navigasi pulih setelah satu koreksi.

## Tabel hasil (isi di salinan lokal, bukan di repo)

| Uji | Titik / pasangan | Ulangan | Sampai? | Jarak sebenarnya (m) | Galat posisi T1 (m) | Durasi (s) | Koreksi diterima | Lompatan > 3 m | Catatan |
|---|---|---|---|---|---|---|---|---|---|
```

- [ ] **Step 2: Jalankan uji dan tarik log**

```powershell
adb pull /sdcard/Android/data/com.rockhead07.eutopos/files/logs "D:\Dev\Projects\eutopos-mobile\device-logs"
```

Expected: satu berkas JSONL per sesi berisi `session_start`, `pose`, `correction`, `route`, `arrival`, `ground_truth`, `tracking`.

- [ ] **Step 3: Commit prosedur (titik commit yang diusulkan)**

```bash
git add docs/field/2026-10-mvp-uji-lapangan.md
git commit -m "docs: field test procedure for the navigation MVP"
```
