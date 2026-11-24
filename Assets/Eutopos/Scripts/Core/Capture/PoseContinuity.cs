using System.Numerics;

namespace Eutopos.Core.Capture
{
    public sealed class PoseContinuity
    {
        // Ambang diagnostik awal dari rekaman jalan, dapat disetel lewat replay per frame.
        public const float MinJumpM = 0.5f;
        public const float MinJumpSpeedMps = 1.4f;
        public int Segment { get; private set; }
        public string Event { get; private set; }
        public float Speed { get; private set; }

        Vector3 previousPosition;
        double previousTime;
        bool hasSample, waitingForConfirmation;

        public bool Observe(Vector3 position, double now, bool tracking)
        {
            Event = null;
            Speed = 0;
            if (!tracking)
            {
                if (hasSample)
                {
                    Segment++;
                    Event = "tracking_lost";
                    waitingForConfirmation = true;
                }
                hasSample = false;
                return false;
            }

            if (hasSample && now > previousTime)
            {
                float distance = Vector3.Distance(position, previousPosition);
                Speed = distance / (float)(now - previousTime);
                waitingForConfirmation = distance >= MinJumpM && Speed >= MinJumpSpeedMps;
                if (waitingForConfirmation)
                {
                    Segment++;
                    Event = "position_jump";
                }
            }

            bool trusted = !waitingForConfirmation;
            previousPosition = position;
            previousTime = now;
            hasSample = true;
            return trusted;
        }
    }
}
