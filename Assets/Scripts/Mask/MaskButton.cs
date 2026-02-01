using UnityEngine;

public class MaskButton : MonoBehaviour
{
    public MaskItem maskItem;

    public void SelectMask()
    {
        MaskManager.instance.SetMask(maskItem.id);
    }
}
