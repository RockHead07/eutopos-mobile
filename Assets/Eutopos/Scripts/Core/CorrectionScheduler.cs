namespace Eutopos.Core
{
    /// <summary>Satu permintaan koreksi dalam satu waktu; timer hanya saat navigasi dan pelacakan aktif.</summary>
    public sealed class CorrectionScheduler
    {
        public float IntervalS = 5f;
        public bool Navigating { get; set; }
        public bool Tracking { get; set; }
        public bool InFlight { get; private set; }
        /// <summary>Timer diam karena jawaban yang tidak pulih dengan mengulang (401/503); tombol tetap jalan.</summary>
        public bool Halted { get; private set; }

        float lastStartT = float.NegativeInfinity;

        public bool ShouldFireTimer(float t) => Navigating && Tracking && !Halted && !InFlight && t - lastStartT >= IntervalS;

        public void Halt() => Halted = true;

        public void Resume() => Halted = false;

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
