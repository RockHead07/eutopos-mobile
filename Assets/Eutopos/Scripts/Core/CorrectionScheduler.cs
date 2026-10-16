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
