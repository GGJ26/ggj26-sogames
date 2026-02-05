using UnityEngine;

public class ZoneTrigger : MonoBehaviour {
    [SerializeField] private bool isPlayer = false;
    /*[HideInInspector]*/ public Zone currentZone;

    void OnTriggerEnter2D(Collider2D collision)
    {
        Zone zone = collision.GetComponent<Zone>();

        if(zone == null) return;

        currentZone = zone;
        if(isPlayer) ZoneManager.Instance.playerCurrentZone = zone;
    }
}