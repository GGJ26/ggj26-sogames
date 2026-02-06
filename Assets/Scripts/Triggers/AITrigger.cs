using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class AITrigger : MonoBehaviour {

    public string action;
    public string [] enemyTypes;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        EnemyMovement enemyMovement = collision.GetComponentInParent<EnemyMovement>();

        if(enemyMovement == null)
            return;

        if(!enemyTypes.Contains(enemyMovement.enemyType))
            return;

        enemyMovement.HandleActionOnTriggerEnter(action);
    }
    
}