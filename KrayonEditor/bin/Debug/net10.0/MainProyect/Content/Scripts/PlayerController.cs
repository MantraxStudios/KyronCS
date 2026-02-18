using KrayonCore;
using KrayonCore.Animation;
using KrayonCore.Components.Components;
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
    public Transform _Camera;
    private bool _isGrounded;
    public bool punched = false;

    public float MouseSensitivity = 0.1f;
    private float _yaw   = 0f;          // rotación acumulada del cuerpo en Y (grados)
    private float _pitch = 0f;          // rotación acumulada de la cámara en X (grados)
    private float _pitchLimit = 89f;

    public override void Start()
    {
        _body    = GameObject.Transform.GetComponent<Rigidbody>();
        _Camera  = SceneManager.ActiveScene.FindGameObjectWithTag("MainCamera").Transform;
        GraphicsEngine.Instance.Window.CursorState = OpenTK.Windowing.Common.CursorState.Grabbed;
    }

    public override void Update(float deltaTime)
    {
        // ── Input de teclado ──────────────────────────────────────
        float inputX = 0f;
        float inputZ = 0f;

        if (InputSystem.GetKeyDown(Keys.W)) inputZ += 1f;
        if (InputSystem.GetKeyDown(Keys.S)) inputZ -= 1f;
        if (InputSystem.GetKeyDown(Keys.A)) inputX -= 1f;
        if (InputSystem.GetKeyDown(Keys.D)) inputX += 1f;

        // ── Mouse ─────────────────────────────────────────────────
        Vector2 mouseDelta = InputSystem.GetMouseDelta();

        // Yaw: acumular y aplicar con SetRotation → sincroniza el Rigidbody
        _yaw -= mouseDelta.X * MouseSensitivity;
        GameObject.Transform.SetRotation(
            Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(_yaw)));

        // Pitch: acumular con clamp y aplicar a la cámara
        _pitch -= mouseDelta.Y * MouseSensitivity;
        _pitch  = Math.Clamp(_pitch, -_pitchLimit, _pitchLimit);
        _Camera.SetRotation(
            Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(_pitch)));

        // ── Movimiento relativo al yaw del jugador ────────────────
        // Usamos el Forward/Right del cuerpo (no de la cámara) para que
        // el movimiento ignore el pitch y solo responda al yaw.
        Vector3 forward = GameObject.Transform.Forward;
        forward.Y = 0f;
        if (forward.LengthSquared > 0f) forward.Normalize();

        Vector3 right = GameObject.Transform.Right;
        right.Y = 0f;
        if (right.LengthSquared > 0f) right.Normalize();

        Vector3 moveDir = forward * inputZ + right * inputX;
        if (moveDir.LengthSquared > 0f)
            moveDir.Normalize();

        Vector3 currentVelocity = _body.GetVelocity();
        Vector3 horizontal      = punched ? Vector3.Zero : moveDir * MoveSpeed;
        _body.SetVelocity(new Vector3(horizontal.X, currentVelocity.Y, horizontal.Z));

        // ── Salto ─────────────────────────────────────────────────
        if (InputSystem.GetKeyPressed(Keys.Space) && _isGrounded)
        {
            _body.AddImpulse(new Vector3(0f, JumpForce, 0f));
            _isGrounded = false;
        }
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