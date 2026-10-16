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
