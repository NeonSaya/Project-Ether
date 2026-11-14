using System;
using UnityEngine;

namespace OsuVR
{
    /// <summary>只转发已接受的谱面反馈，不参与计分、音效、震动或输入。</summary>
    public static class RayHitFeedback
    {
        public static event Action<bool, Transform> Hit;
        public static event Action<bool> Tracking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Hit = null;
            Tracking = null;
        }

        public static void NotifyHit(bool isRightHand, Transform surface = null) =>
            Hit?.Invoke(isRightHand, surface);

        public static void NotifyTracking(bool isRightHand) => Tracking?.Invoke(isRightHand);
    }
}
