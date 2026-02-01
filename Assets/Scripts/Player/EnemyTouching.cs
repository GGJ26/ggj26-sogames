using UnityEngine;

public class EnemyTouching : MonoBehaviour
{
    [Tooltip("Layers considérés comme des ennemis")]
    public LayerMask enemyLayer;

        [Header("Follow settings")]
        private bool followPosition = true;
        private bool followRotation = false;
        private bool followScale = false;

        private Transform _target;   // ancien parent
        private Vector3 _localOffset;
        private Quaternion _localRotation;
        private Vector3 _localScale;

    private void Awake()
    {
        // On mémorise le parent actuel
        _target = transform.parent;

        if (_target != null)
        {
            // Sauvegarde des offsets locaux
            _localOffset = transform.localPosition;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;

            // Détachement
            transform.SetParent(null, true);
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        if (followPosition)
            transform.position = _target.TransformPoint(_localOffset);

        if (followRotation)
            transform.rotation = _target.rotation * _localRotation;

        if (followScale)
            transform.localScale = Vector3.Scale(_target.localScale, _localScale);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }
}
