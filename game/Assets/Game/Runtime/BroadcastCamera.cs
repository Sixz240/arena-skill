using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>Câmera de TV: na lateral, alta, seguindo a bola com atraso suave.</summary>
    public sealed class BroadcastCamera : MonoBehaviour
    {
        public Transform Target;
        public float Height = 19f;
        public float Distance = 34f;   // atrás da linha lateral -Z
        public float Smooth = 2.4f;
        private Vector3 _look;

        private void LateUpdate()
        {
            if (Target == null) return;
            var b = Target.position;
            var desiredLook = new Vector3(Mathf.Clamp(b.x, -Pitch.HalfLength + 12f, Pitch.HalfLength - 12f), 0, Mathf.Clamp(b.z * 0.6f, -14f, 14f));
            _look = _look == Vector3.zero ? desiredLook : Vector3.Lerp(_look, desiredLook, 1 - Mathf.Exp(-Smooth * Time.deltaTime));
            var pos = new Vector3(_look.x * 0.92f, Height, -Pitch.HalfWidth - Distance + _look.z * 0.3f);
            transform.position = pos;
            transform.LookAt(_look + new Vector3(0, 0, 4f));
        }
    }
}
