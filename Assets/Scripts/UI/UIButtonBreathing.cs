using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIButtonBreathing : MonoBehaviour
{
    [Header("Scale")]
    public float scaleAmplitude = 0.06f;   // 6% max
    public float scaleSpeed = 1.2f;

    [Header("Rotation")]
    public float rotationAmplitude = 1.2f; // degrés
    public float rotationSpeed = 0.8f;

    [Header("Offset (optional)")]
    public float verticalOffset = 2f;      // pixels
    public float offsetSpeed = 1.0f;

    private RectTransform _rect;
    private Vector3 _baseScale;
    private Vector3 _baseRotation;
    private Vector2 _baseAnchoredPos;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _baseScale = _rect.localScale;
        _baseRotation = _rect.localEulerAngles;
        _baseAnchoredPos = _rect.anchoredPosition;
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        // Scale (breathing)
        float scale = 1f + Mathf.Sin(t * scaleSpeed) * scaleAmplitude;
        _rect.localScale = _baseScale * scale;

        // Rotation (micro wobble)
        float rotZ = Mathf.Sin(t * rotationSpeed) * rotationAmplitude;
        _rect.localEulerAngles = new Vector3(
            _baseRotation.x,
            _baseRotation.y,
            _baseRotation.z + rotZ
        );

        // Vertical float (optional)
        if (verticalOffset > 0.01f)
        {
            float y = Mathf.Sin(t * offsetSpeed) * verticalOffset;
            _rect.anchoredPosition = _baseAnchoredPos + Vector2.up * y;
        }
    }
}
