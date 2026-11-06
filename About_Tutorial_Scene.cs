using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class About_Tutorial_Scene : MonoBehaviour
{
    public void Back()
    {
        SceneManager.LoadScene("SampleScene");
    }
}
