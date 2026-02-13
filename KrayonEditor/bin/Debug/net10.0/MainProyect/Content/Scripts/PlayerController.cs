using KrayonCore;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PlayerController : KrayonBehaviour
{
    public float JumpForce = 10.0f;

    public override void Start()
    {
        GraphicsEngine.Instance.GetSceneRenderer().GetCamera().SetProjectionMode(ProjectionMode.Orthographic);
    }

    public override void Update(float deltaTime)
    {
        if (InputSystem.GetKeyPressed(Keys.Space))
        {
            GameObject.GetComponent<Rigidbody>().SetVelocity(Vector3.Zero);
            GameObject.GetComponent<Rigidbody>().AddForce(new Vector3(0, MathF.Sqrt(JumpForce) * 2.0f, 0));
            GameObject.GetComponent<AudioSource>().Play();
        }
    }
}
