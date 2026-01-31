using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Sound Manager", menuName = "Scriptable Objects/Sound Manager")]
public class SoundManagerSO : ScriptableObject
{
    private static SoundManagerSO instance;
    public static SoundManagerSO Instance
    { 
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<SoundManagerSO>("Sound Manager");
            }
            return instance;
        }
    }
    public AudioSource SoundObject;
    public static float _volumeChangeMultiplier = 0.15f;
    public static float _pitchChangeMultiplier = 0.1f;

    public static void PlaySoundFXClip(AudioClip clip, Vector3 soundPos, float volume)
    {
        float randVolume = Random.Range(volume - _volumeChangeMultiplier, volume + _volumeChangeMultiplier);
        float randPitch = Random.Range(1 - _pitchChangeMultiplier, 1 + _pitchChangeMultiplier);
        AudioSource a = Instantiate(Instance.SoundObject, soundPos, Quaternion.identity);

        a.clip = clip;
        a.volume = randVolume;
        a.pitch = randPitch;
        a.Play();
    }
    public static void PlayRandomSoundFXClip(AudioClip[] clips, Vector3 soundPos, float volume)
    {
        int randClip = Random.Range(0, clips.Length);
        float randVolume = Random.Range(volume - _volumeChangeMultiplier, volume + _volumeChangeMultiplier);
        float randPitch = Random.Range(1 - _pitchChangeMultiplier, 1 + _pitchChangeMultiplier);
        AudioSource a = Instantiate(Instance.SoundObject, soundPos, Quaternion.identity);

        a.clip = clips[randClip];
        a.volume = randVolume;
        a.pitch = randPitch;
        a.Play();
    }

}

