using UnityEngine;

public class INFINITEPARALLAXE : MonoBehaviour
{
    public Transform target;
    [Range(0f, 1f)] public float parallaxFactor = 0.3f;

    private Transform[] tiles;
    private float tileWidth;
    private float tileHeight;
    private Vector3 lastTargetPos;

    void Start()
    {
        if(target == null)
            target = Camera.main.transform;
        
        lastTargetPos = target.position;

        tiles = new Transform[transform.childCount];
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = transform.GetChild(i);

        // Calcul automatique de la taille du sprite
        SpriteRenderer sr = tiles[0].GetComponent<SpriteRenderer>();
        tileWidth = sr.sprite.bounds.size.x * sr.transform.lossyScale.x;
        tileHeight = sr.sprite.bounds.size.y * sr.transform.lossyScale.y;
    }

    void LateUpdate()
    {
        Vector3 delta = target.position - lastTargetPos;

        // Suivi du layer avec parallaxe
        transform.position += new Vector3(
            delta.x * parallaxFactor,
            delta.y * parallaxFactor,
            0f
        );

        lastTargetPos = target.position;

        foreach (Transform t in tiles)
        {
            float diffX = target.position.x - t.position.x;
            float diffY = target.position.y - t.position.y;

            if (Mathf.Abs(diffX) >= tileWidth)
                t.position += Vector3.right * Mathf.Sign(diffX) * tileWidth * 2f;

            if (Mathf.Abs(diffY) >= tileHeight)
                t.position += Vector3.up * Mathf.Sign(diffY) * tileHeight * 2f;
        }
    }
}