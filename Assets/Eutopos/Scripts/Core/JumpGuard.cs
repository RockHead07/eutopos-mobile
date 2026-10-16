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
