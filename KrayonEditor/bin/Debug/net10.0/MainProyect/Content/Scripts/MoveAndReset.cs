using KrayonCore;
using OpenTK.Mathematics;

public class MoveAndReset : KrayonBehaviour
{
    public float _MoveSpeed = 5.0f;

    public override void Update(float deltaTime)
    {
        Vector3 _Pos = GameObject.Transform.GetWorldPosition();

        _Pos.X -= _MoveSpeed * deltaTime;

        GameObject.Transform.SetPosition(_Pos);
    }

    public override void OnTriggerStay(GameObject other)
    {
        if (other.Name == "Player")
        {
            Console.WriteLine("Player Not Found");

            SceneManager.LoadScene("Content/scenes/DefaultScene.scene");
        }
        else
        {
            Console.WriteLine("Player Not Found");
        }
    }
}