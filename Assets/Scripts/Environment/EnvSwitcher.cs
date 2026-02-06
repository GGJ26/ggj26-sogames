using System;
using UnityEngine;

public class EnvSwitcher : MonoBehaviour
{
    [SerializeField] private EnvAndId [] envAndNames;

    private void OnEnable()
    {
        MaskManager.instance.AddMaskListener(SwitchEnvironemt);
    }

    private void OnDisable()
    {
        MaskManager.instance.RemoveMaskListener(SwitchEnvironemt);
    }

    private void SwitchEnvironemt()
    {
        for (int i = 0; i < envAndNames.Length; i++)
        {
            string n1 = envAndNames[i].id;
            string n2 = MaskManager.instance.selectedMask.name;
            envAndNames[i].envContainer
                .SetActive(envAndNames[i].id == MaskManager.instance.selectedMask.id);
        }
    }
}

[Serializable]
public class EnvAndId
{
    public string id;
    public GameObject envContainer;
}
