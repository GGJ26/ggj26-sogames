using UnityEngine;

public class MaskButton : MonoBehaviour
{
    public MaskItem maskItem;

    public void SelectMask()
    {
        WillManagerTest.instance.SetMask(maskItem.id);
    }
}
