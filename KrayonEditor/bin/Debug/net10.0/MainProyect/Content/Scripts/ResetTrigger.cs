using KrayonCore;
using OpenTK.Mathematics;

public class ResetTrigger : KrayonBehaviour
{
    public override void OnTriggerStay(GameObject other)
    {
        if (other.Name == "Player")
        {
            SceneManager.LoadScene("Content/scenes/DefaultScene.scene");
        }
    }
}
