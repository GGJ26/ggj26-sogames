using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMaskItem",
    menuName = "Game/Mask Item",
    order = 0
)]
public class MaskItem : ScriptableObject
{
    public string id;
    public Sprite sprite;
}
