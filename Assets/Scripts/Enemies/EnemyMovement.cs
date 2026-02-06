using UnityEngine;

public abstract class EnemyMovement : MonoBehaviour {
    public string enemyType;
    public ZoneTrigger zoneTrigger;


    [Tooltip("Animator wrapper (uses triggers: run / idle / jump).")]
    public PlayerAnimator playerAnimator;
    [Header("Visual")]
    public Transform visualContainer;


    public abstract void HandleActionOnTriggerEnter(string action);
}