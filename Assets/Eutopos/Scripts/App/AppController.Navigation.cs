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
