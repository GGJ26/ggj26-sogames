using System;
using UnityEngine;

public class PathManager : MonoBehaviour {
    #region Instance 
    public static PathManager _instance;

    public static PathManager instance // Mettre une majuscule
    {
        get{
            if (_instance == null)
            {
                // 1️⃣ On tente de le trouver dans la scène
                _instance = FindFirstObjectByType<PathManager>();

                // Fallback pour anciennes versions Unity
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PathManager>();
                }

                // 2️⃣ Sécurité : aucun trouvé
                if (_instance == null)
                {
                    Debug.LogError("[PathManager] Aucun PathManager trouvé dans la scène !");
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
        MaskManager.instance.AddMaskListener(HandleMaskChange);
    }
#endregion

    public Path[] paths;

    private void HandleMaskChange()
    {
        for (int i = 0; i < paths.Length; i++)
        {
            paths[i].gameObject.SetActive(paths[i].maskItem == MaskManager.instance.selectedMask);
        }
    }

    public Path GetPath(MaskItem maskItem)
    {
        return Array.Find(paths, p => p.maskItem == maskItem);
    }
}