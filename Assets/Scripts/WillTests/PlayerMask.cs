using UnityEngine;
using System.Collections;

public class PlayerMask : MonoBehaviour {
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void OnEnable() {
        StartCoroutine(AddMaskListener());
    }

    private IEnumerator AddMaskListener()
    {
        while(WillManagerTest.instance == null)
            yield return null;

        WillManagerTest.instance.AddMaskListener(ChangeMask);
    }

    private void OnDisable() {
        WillManagerTest.instance.RemoveMaskListener(ChangeMask);
    }

    private void ChangeMask()
    {
        spriteRenderer.sprite = WillManagerTest.instance.selectedMask.sprite;
    }
}