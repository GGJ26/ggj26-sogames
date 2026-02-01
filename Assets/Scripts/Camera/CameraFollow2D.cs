using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    [Tooltip("Vitesse de lissage de la caméra")]
    public float smoothTime = 0.2f;

    [Tooltip("Offset fixe de la caméra")]
    public Vector2 baseOffset = new Vector2(0f, 1.5f);

    [Header("Look Ahead")]
    [Tooltip("Distance max de regard dans la direction du mouvement")]
    public float lookAheadDistance = 2f;

    [Tooltip("Vitesse de lissage du look-ahead")]
    public float lookAheadSmooth = 0.15f;

    private Vector3 _velocity = Vector3.zero;
    private float _lookAheadX;
    private float _lookAheadVelocity;

    private Vector3 _lastTargetPos;

    private void Start()
    {
        target = PlayerMovement.instance.container;
        if (target != null)
            _lastTargetPos = target.position;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Déplacement du joueur depuis la frame précédente
        float deltaX = target.position.x - _lastTargetPos.x;

        // Look-ahead horizontal (anticipation du mouvement)
        float targetLookAheadX = Mathf.Sign(deltaX) * lookAheadDistance;
        if (Mathf.Abs(deltaX) < 0.001f)
            targetLookAheadX = 0f;

        _lookAheadX = Mathf.SmoothDamp(
            _lookAheadX,
            targetLookAheadX,
            ref _lookAheadVelocity,
            lookAheadSmooth
        );

        Vector3 desiredPos = new Vector3(
            target.position.x + _lookAheadX + baseOffset.x,
            target.position.y + baseOffset.y,
            transform.position.z
        );

        // Lissage principal de la caméra
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref _velocity,
            smoothTime
        );

        _lastTargetPos = target.position;
    }
}
