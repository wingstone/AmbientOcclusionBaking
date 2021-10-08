using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SetAODisplay : MonoBehaviour
{
   public  Texture2D[] AOTexs;
    // Start is called before the first frame update
    void Start()
    {
        LightmapData[] lightmapDatas = LightmapSettings.lightmaps;
        int len = Mathf.Min(lightmapDatas.Length, AOTexs.Length);

        for (int i = 0; i < len; i++)
        {
            lightmapDatas[i].lightmapColor = AOTexs[i];
        }

        LightmapSettings.lightmaps = lightmapDatas;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
