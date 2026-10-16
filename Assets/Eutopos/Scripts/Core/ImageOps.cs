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
