using System;
using System.Collections.Generic;
using System.Numerics;

namespace Eutopos.Core.Capture
{
    public enum ShotDecision
    {
        Capture, NotTracking, TooFast, BelowThreshold, LimitReached, LowStorage, TooCloseToTest,
    }

    public static class CapturePolicy
    {
        public const float MinMoveM = 0.4f;
        public const float MinTurnDeg = 10f;
        public const float MaxSpeedMps = 0.7f;
        public const int MaxPhotos = 1500;
        public const long MinFreeBytes = 1L << 30;
        public const int MinTestPhotos = 5;
        public const float MinTestSpacingM = 1f;

        static readonly float DotAtMinTurn = (float)Math.Cos(MinTurnDeg * Math.PI / 360);

        public static ShotDecision Auto(bool tracking, float speed, Vector3 pos, Quaternion rot,
            bool hasPrevious, Vector3 prevPos, Quaternion prevRot, int count, long freeBytes)
        {
            var decision = Manual(tracking, speed, count, freeBytes);
            if (decision != ShotDecision.Capture) return decision;

            // q dan -q menyatakan orientasi yang sama.
            if (!hasPrevious || Vector3.DistanceSquared(pos, prevPos) >= MinMoveM * MinMoveM ||
                Math.Abs(Quaternion.Dot(rot, prevRot)) <= DotAtMinTurn)
                return ShotDecision.Capture;

            return ShotDecision.BelowThreshold;
        }

        public static ShotDecision Manual(bool tracking, float speed, int count, long freeBytes)
        {
            if (!tracking) return ShotDecision.NotTracking;
            if (count >= MaxPhotos) return ShotDecision.LimitReached;
            if (freeBytes < MinFreeBytes) return ShotDecision.LowStorage;
            return speed > MaxSpeedMps ? ShotDecision.TooFast : ShotDecision.Capture;
        }

        public static ShotDecision TestShot(bool tracking, float speed, Vector3 pos,
            IReadOnlyList<Vector3> previousTests)
        {
            if (!tracking) return ShotDecision.NotTracking;
            if (speed > MaxSpeedMps) return ShotDecision.TooFast;
            foreach (var previous in previousTests)
                if (Vector3.DistanceSquared(pos, previous) < MinTestSpacingM * MinTestSpacingM)
                    return ShotDecision.TooCloseToTest;

            return ShotDecision.Capture;
        }

        public static float DistanceM(IReadOnlyList<FrameRecord> frames)
        {
            float distance = 0;
            for (int i = 1; i < frames.Count; i++)
                distance += Vector3.Distance(frames[i - 1].Position, frames[i].Position);
            return distance;
        }

        public static bool AreaExists(string id, IReadOnlyList<BuildingInfo> buildings)
        {
            foreach (var building in buildings)
                foreach (string areaId in building.AreaIds)
                    if (string.Equals(areaId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        public static string Reason(ShotDecision d) => d switch
        {
            ShotDecision.NotTracking => "Pelacakan belum siap",
            ShotDecision.TooFast => "Terlalu cepat, pelankan",
            ShotDecision.LimitReached => "Batas 1500 foto tercapai",
            ShotDecision.LowStorage => "Ruang penyimpanan kurang dari 1 GB",
            ShotDecision.TooCloseToTest => "Terlalu dekat dengan foto uji lain (min 1 m)",
            _ => null,
        };
    }

    public sealed class CaptureTrackingGate
    {
        double? stableSince;

        public bool Observe(double now, bool tracking, bool discontinuity)
        {
            if (!tracking || discontinuity)
            {
                stableSince = null;
                return false;
            }

            stableSince ??= now;
            return now >= stableSince.Value + 1;
        }
    }
}
