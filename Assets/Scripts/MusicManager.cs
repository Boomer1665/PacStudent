using UnityEngine;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    public AudioSource musicSource;
    public AudioClip introClip;
    public AudioClip normalClip;
    public AudioClip scaredClip;
    public AudioClip deadClip;

    void Start()
    {
        StartCoroutine(PlayIntroThenNormal());
    }

    IEnumerator PlayIntroThenNormal()
    {
        musicSource.clip = introClip;
        musicSource.loop = false;
        musicSource.Play();

        yield return new WaitForSeconds(3f);

        PlayNormal();
    }

    public void PlayNormal() { Play(normalClip); }
    public void PlayScared() { Play(scaredClip); }
    public void PlayDead() { Play(deadClip); }

    void Play(AudioClip clip)
    {
        // Don't restart a clip that is already playing
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}