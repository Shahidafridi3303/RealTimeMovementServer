using UnityEngine;

public class GameLogic : MonoBehaviour
{
    void Start()
    {
        NetworkServerProcessing.SetGameLogic(this);
    }

    public static void SetGameLogic(GameLogic logic)
    {
        NetworkServerProcessing.SetGameLogic(logic);
    }
}