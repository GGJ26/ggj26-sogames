using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMaskItem",
    menuName = "Game/Mask Item",
    order = 0
)]
public class MaskItem : ScriptableObject
{
    public enum MaskEffectType
    {
        InvertedGravity = 1,   // Gravité inversée
        LowGravityJump = 2,    // Saut plus long / gravité réduite
        PhaseThrough = 3       // Passage à travers les objets
    }

    public string id;
    public Sprite sprite;
    public MaskEffectType maskEffectType;
}
