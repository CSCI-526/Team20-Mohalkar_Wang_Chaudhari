using UnityEngine;

namespace Lockdown {

/// <summary>With no audio and no slow-mo, this is the loudest feedback left. Expect to
/// tune the magnitudes up.</summary>
public class CameraShake : MonoBehaviour {
    public static CameraShake I { get; private set; }

    Vector3 _home;
    float _mag, _until, _dur;

    void Awake() { I = this; _home = transform.localPosition; }
    void OnDestroy() { if (I == this) I = null; }

    public void Shake(float magnitude, float duration) {
        _mag = Mathf.Max(_mag, magnitude);
        _dur = duration;
        _until = Time.unscaledTime + duration;
    }

    void LateUpdate() {
        if (Time.unscaledTime >= _until) { transform.localPosition = _home; _mag = 0f; return; }
        float falloff = _dur <= 0f ? 0f : (_until - Time.unscaledTime) / _dur;
        transform.localPosition = _home + (Vector3)(Random.insideUnitCircle * _mag * falloff);
    }
}
}
