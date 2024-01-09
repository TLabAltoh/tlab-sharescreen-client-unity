using UnityEngine;

public class TLabPlayManeger : MonoBehaviour
{
    void Start()
    {
        switch (Screen.orientation)
        {
            case ScreenOrientation.Portrait:
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                break;
            case ScreenOrientation.PortraitUpsideDown:
                Screen.orientation = ScreenOrientation.LandscapeRight;
                break;
        }
    }
}
