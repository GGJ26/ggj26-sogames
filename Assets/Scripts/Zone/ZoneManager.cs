using UnityEngine;

public class ZoneManager : MonoBehaviour {
    [SerializeField] private Zone[] zones;

    /*[HideInInspector]*/ public Zone playerCurrentZone;

#region Instance 
    public static ZoneManager _instance;

    public static ZoneManager Instance
    {
        get{
            if (_instance == null)
            {
                // 1️⃣ On tente de le trouver dans la scène
                _instance = FindFirstObjectByType<ZoneManager>();

                // Fallback pour anciennes versions Unity
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ZoneManager>();
                }

                // 2️⃣ Sécurité : aucun trouvé
                if (_instance == null)
                {
                    Debug.LogError("[ZoneManager] Aucun ZoneManager trouvé dans la scène !");
                }
            }

            return _instance;
        }
    }
    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        _instance = null;      
    }
    #endregion
}