using KrayonCore;
using KrayonCore.Core;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PlayerController : KrayonBehaviour
{
    public float MoveSpeed = 1.0f;
    private Rigidbody _body;

    public override void Start()
    {
        GetMainCamera().ProjectionMode = ProjectionMode.Orthographic;
        GetMainCamera().OrthoSize = 15.0f;

        Vector3 pos = GetMainCamera().Position;
        pos.X = GameObject.Transform.GetWorldPosition().X;
        pos.Y = GameObject.Transform.GetWorldPosition().Y;
        GetMainCamera().Position = pos;

        _body = GameObject.GetComponent<Rigidbody>();
    }

    public override void Update(float deltaTime)
    {
        Vector3 direction = Vector3.Zero;

        if (InputSystem.GetKeyDown(Keys.A))
        {
            direction.X -= 1.0f;
            GameObject.GetComponent<SpriteRenderer>().FlipX = true;
        }
        
        if (InputSystem.GetKeyDown(Keys.D))
        {
            direction.X += 1.0f;
            GameObject.GetComponent<SpriteRenderer>().FlipX = false;
        }

        if (InputSystem.GetKeyDown(Keys.W))
        {
            direction.Y += 1.0f;
        }

        if (InputSystem.GetKeyDown(Keys.S))
        {
            direction.Y -= 1.0f;
        }

        if (direction != Vector3.Zero)
        {
            direction = direction.Normalized();
            _body.SetVelocity(direction * MoveSpeed);
            GameObject.GetComponent<SpriteRenderer>().Play("Walk");
        }
        else
        {
            GameObject.GetComponent<SpriteRenderer>().Play("Idle");
            _body.SetVelocity(Vector3.Zero);
        }
    }
}