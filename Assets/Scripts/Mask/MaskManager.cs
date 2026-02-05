using System;
using UnityEngine;
using UnityEngine.Events;

public class MaskManager : MonoBehaviour
{
#region Instance 
    public static MaskManager _instance;

    public static MaskManager instance // Mettre une majuscule
    {
        get{
            if (_instance == null)
            {
                // 1️⃣ On tente de le trouver dans la scène
                _instance = FindFirstObjectByType<MaskManager>();

                // Fallback pour anciennes versions Unity
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MaskManager>();
                }

                // 2️⃣ Sécurité : aucun trouvé
                if (_instance == null)
                {
                    Debug.LogError("[MaskManager] Aucun MaskManager trouvé dans la scène !");
                }
            }

            return _instance;
        }
    }
    private void Awake()
    {
        // Protection contre les doublons
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }
#endregion

    public MaskItem selectedMask;
    private UnityEvent changeMaskEvent = new();
    [SerializeField] private MaskItem[] maskItems;
    [SerializeField] private MaskAndDaemonInstances[] maskAndDaemonInstances;

    private void Start() {
        SetMask(selectedMask.id);
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
        HandleDeamonsState();
        changeMaskEvent?.Invoke();
    }

    private void HandleDeamonsState()
    {
        for (int i = 0; i < maskAndDaemonInstances.Length; i++)
        {
            MaskItem maskItem = maskAndDaemonInstances[i].maskItem;
            GameObject[] daemons = maskAndDaemonInstances[i].daemons;
            for (int j = 0; j < daemons.Length; j++)
            {
                daemons[j].SetActive(maskItem == selectedMask);
            }
        }
    }
}

[Serializable]
public class MaskAndDaemonInstances
{
    public string name; // à titre indicatif
    public MaskItem maskItem;
    public GameObject [] daemons;
}
