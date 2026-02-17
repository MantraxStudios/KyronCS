using KrayonCore;
using KrayonCore.Animation;
using KrayonCore.Core;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PlayerController : KrayonBehaviour
{
    public float MoveSpeed = 5.0f;
    public float JumpForce = 4.0f;

    private Rigidbody _body;
    private bool _isGrounded;
    public bool punched = false;

    public override void Start()
    {
        _body = GameObject.Transform.Parent.GetComponent<Rigidbody>();
    }

    public override void Update(float deltaTime)
    {
        float inputX = 0f;
        float inputZ = 0f;

        if (InputSystem.GetKeyDown(Keys.W)) inputZ += 1f;
        if (InputSystem.GetKeyDown(Keys.S)) inputZ -= 1f;
        if (InputSystem.GetKeyDown(Keys.A)) inputX -= 1f;
        if (InputSystem.GetKeyDown(Keys.D)) inputX += 1f;

        bool isMoving = (inputX != 0f || inputZ != 0f);

        float horizontalX = inputX * MoveSpeed;
        float horizontalZ = inputZ * MoveSpeed;

        if (punched)
        {
            horizontalX = 0f;
            horizontalZ = 0f;
        }

        Vector3 currentVelocity = _body.GetVelocity();
        _body.SetVelocity(new Vector3(horizontalX, currentVelocity.Y, horizontalZ));

        if (InputSystem.GetKeyPressed(Keys.Space) && _isGrounded)
        {
            GameObject.GetComponent<Animator>().PlayCrossFade(5);
            _body.AddImpulse(new Vector3(0f, JumpForce, 0f));
            _isGrounded = false;
        }

        if (InputSystem.GetMouseButtonDown(MouseButton.Left) && !punched && _isGrounded)
        {
            GameObject.GetComponent<Animator>().PlayCrossFade(2);
            Invoke("GoBackIdle", BackToIdleAnimation, 1200);
            punched = true;
        }

        if (!punched && _isGrounded)
        {
            if (isMoving)
                GameObject.GetComponent<Animator>().PlayCrossFade(3);
            else
                GameObject.GetComponent<Animator>().PlayCrossFade(1);
        }
    }

    public void BackToIdleAnimation()
    {
        GameObject.GetComponent<Animator>().PlayCrossFade(1);
        punched = false;
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