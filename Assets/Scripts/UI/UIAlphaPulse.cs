using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIImageAlphaPulse : MonoBehaviour
{
    [Range(0f, 1f)]
    public float minAlpha = 0.7f;

    [Range(0f, 1f)]
    public float maxAlpha = 1f;

    public float speed = 1.2f;

    private Image _image;
    private Color _baseColor;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _baseColor = _image.color;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float a = Mathf.Lerp(
            minAlpha,
            maxAlpha,
            (Mathf.Sin(t * speed) + 1f) * 0.5f
        );

        Color c = _baseColor;
        c.a = a;
        _image.color = c;
    }
}
