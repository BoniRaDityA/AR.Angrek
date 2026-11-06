using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Play()
    {
        SceneManager.LoadScene("Play");
    }

    public void Tutorial()
        {
            SceneManager.LoadScene("Tutorial");
        }

    public void About()
    {
        SceneManager.LoadScene("About");
    }

    public void Exit()
    {
        Application.Quit();
        Debug.Log("Berhasil Keluar Aplikasi");
    }
}
