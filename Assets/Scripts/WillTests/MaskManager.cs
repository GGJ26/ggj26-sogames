using System;
using UnityEngine;
using UnityEngine.Events;

public class MaskManager : MonoBehaviour
{ 
    public static MaskManager instance;
    public MaskItem selectedMask;
    private UnityEvent changeMaskEvent = new();
    [SerializeField] private MaskItem[] maskItems;

    private void Awake() {
        instance = this;
    }

    public void AddMaskListener(UnityAction action)
    {
        changeMaskEvent.AddListener(action);
    }

    public void RemoveMaskListener(UnityAction action)
    {
        changeMaskEvent.RemoveListener(action);
    }

    public void SetMask(string id)
    {
        selectedMask = Array.Find(maskItems, m => m.id == id);
        changeMaskEvent?.Invoke();
    }
}
