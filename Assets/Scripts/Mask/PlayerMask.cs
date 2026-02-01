using UnityEngine;
using System.Collections;

public class PlayerMask : MonoBehaviour {
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void OnEnable() {
        StartCoroutine(AddMaskListener());
    }

    private IEnumerator AddMaskListener()
    {
        while(MaskManager.instance == null)
            yield return null;

        MaskManager.instance.AddMaskListener(ChangeMask);
    }

    private void OnDisable() {
        MaskManager.instance.RemoveMaskListener(ChangeMask);
    }

    private void ChangeMask()
    {
        spriteRenderer.sprite = MaskManager.instance.selectedMask.sprite;
    }
}