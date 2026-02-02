using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public Transform player;

    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform layer;
        [Range(0f, 1f)] public float parallaxMultiplier;
    }

    public ParallaxLayer[] layers;

    private Vector3 lastPlayerPosition;

    void Start()
    {
        lastPlayerPosition = player.position;
    }

    void LateUpdate()
    {
        Vector3 delta = player.position - lastPlayerPosition;

        foreach (ParallaxLayer l in layers)
        {
            l.layer.position += new Vector3(
                delta.x * l.parallaxMultiplier,
                delta.y * l.parallaxMultiplier,
                0f
            );
        }

        lastPlayerPosition = player.position;
    }
}