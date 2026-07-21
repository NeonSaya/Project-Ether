using UnityEngine;

namespace CasualHit.VFX
{
    public class CameraShake : MonoBehaviour
    {
        public float shakeDuration = 0.5f;
        public float shakeMagnitude = 0.1f;
        public float dampingSpeed = 1.0f;

        private Vector3 initialPosition;
        private float currentShakeDuration = 0f;

        void OnEnable()
        {
            initialPosition = transform.localPosition;
        }

        void Update()
        {
            if (currentShakeDuration > 0)
            {
                float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
                float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;

                transform.localPosition = initialPosition + new Vector3(offsetX, offsetY, 0f);
                currentShakeDuration -= Time.deltaTime * dampingSpeed;
            }
            else
            {
                transform.localPosition = initialPosition;
                currentShakeDuration = 0f;
            }
        }

        public void TriggerShake()
        {
            currentShakeDuration = shakeDuration;
        }
    }
}