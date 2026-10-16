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
