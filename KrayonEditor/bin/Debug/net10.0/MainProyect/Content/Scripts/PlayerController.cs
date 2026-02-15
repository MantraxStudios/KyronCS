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
    public float JumpForce = 2.0f;

    private Rigidbody _body;
    private bool _isGrounded;

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
        float horizontalVelocity = 0f;

        if (InputSystem.GetKeyDown(Keys.A))
        {
            horizontalVelocity -= MoveSpeed;
            GameObject.GetComponent<SpriteRenderer>().FlipX = true;
        }

        if (InputSystem.GetKeyDown(Keys.D))
        {
            horizontalVelocity += MoveSpeed;
            GameObject.GetComponent<SpriteRenderer>().FlipX = false;
        }

        Vector3 currentVelocity = _body.GetVelocity();
        _body.SetVelocity(new Vector3(horizontalVelocity, currentVelocity.Y, 0f));

        if (InputSystem.GetKeyPressed(Keys.Space) && _isGrounded)
        {
            _body.AddImpulse(new Vector3(0f, JumpForce, 0f));
            _isGrounded = false;
        }

        if (horizontalVelocity != 0f)
            GameObject.GetComponent<SpriteRenderer>().Play("Walk");
        else
            GameObject.GetComponent<SpriteRenderer>().Play("Idle");

        Vector3 worldPos = GameObject.Transform.GetWorldPosition();
        Vector3 camPos = GetMainCamera().Position;
        camPos.X = worldPos.X;
        camPos.Y = worldPos.Y;
        GetMainCamera().Position = camPos;
    }

    public override void OnCollisionEnter(GameObject contact)
    {
            Console.WriteLine($"Colisionando con: {contact.Name}");
            _isGrounded = true;
    }

    public override void OnCollisionExit(GameObject contact)
    {
            Console.WriteLine($"Descolisionando con: {contact.Name}");
            _isGrounded = false;
    }
}