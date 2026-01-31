using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
public class SoundMixerManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    public void SetMasterVolume(float level)
    {
        audioMixer.SetFloat("masterVol", Mathf.Log10(level) * 20f);
    }
    public void SetSFXVolume(float level)
    {
        audioMixer.SetFloat("sfxVol", Mathf.Log10(level) * 20f);
    }
    public void SetMusicVolume(float level)
    {
        audioMixer.SetFloat("musVol", Mathf.Log10(level) * 20f);
    }
    public void SetUIVolume(float level)
    {
        audioMixer.SetFloat("uiVol", Mathf.Log10(level) * 20f);
    }
}
