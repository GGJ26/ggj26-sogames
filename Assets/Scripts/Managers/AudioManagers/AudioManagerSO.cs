using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Audio/Audio Manager", fileName ="AudioManagerSO")]

public class AudioManagerSO : ScriptableObject
{
    private static AudioManagerSO instance; 
    public static AudioManagerSO Instance
    { 
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<AudioManagerSO>("AudioManagerSO");
            }
            return instance;
        }
    }
    
    public AudioSource SoundObject;

    private static float _volumeChangeMultiplier = 0.15f;
    private static float _pitchChangeMultiplier = 0.1f;

    #region Design par ScriptableObject
    /* Le SO permet d'être un asset du jeu et n'a pas besoin d'être en dur dans la hiérarchie.
     * Une fois appelé, il s'instancie et se comporte comme un singleton.
     * Ca permet d'être plus rapide et il y aura moins de bazar dans les scripts audio de cette manière.
     * De plus, ça casse les possibles dépendances entre GameObject et fichiers "clips" unity.
    */

    // "volume" = niveau de sortie du fichier audio.
    // Je réserve le nom "level" pour le niveau du mixeur audio.
    #endregion

    /*Fonction pour spawn un objet son (non GameObject, donc pas de GetComponent !) dans une pool,
     * avec une position, et en lui assignant un volume en float */
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

    /* WIP : Fonction pour spawn un container son dans une pool,
     * avec une position, et en lui assignant un volume en float */

    public static void PlayRandomContainerSoundFXClip(AudioClip clip, Vector3 soundPos, float volume)
    {
        float randVolume = Random.Range(volume - _volumeChangeMultiplier, volume + _volumeChangeMultiplier);
        float randPitch = Random.Range(1 - _pitchChangeMultiplier, 1 + _pitchChangeMultiplier);

        AudioSource a = Instantiate(Instance.SoundObject, soundPos, Quaternion.identity);
        a.clip = clip;
        a.volume = randVolume;
        a.pitch = randPitch;
        a.Play();
    }
}
