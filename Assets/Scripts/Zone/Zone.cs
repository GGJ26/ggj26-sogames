using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Zone : MonoBehaviour {
    [SerializeField] private ZoneAndDirection[] adjacentZones;
}

public enum ZoneDirection { TOP, BOTTOM, LEFT, RIGHT };

[Serializable]
public class ZoneAndDirection
{
    public ZoneDirection zoneDirection;
    public Zone zone;
}