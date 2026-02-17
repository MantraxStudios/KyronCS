using KrayonCore;
using KrayonCore.Components.Components;
using OpenTK.Mathematics;

public class FollowPlayer : KrayonBehaviour
{
    public GameObject _Player;
    public float FollowSpeed = 5f;

    public float ZOffset = 25.0f; // distancia detrás/delante

    public override void Start()
    {
        _Player = GameObject.FindGameObjectsWithTag("CameraTag")[0];
    }

    public override void Update(float deltaTime)
    {
        Vector3 playerPos = _Player.Transform.GetWorldPosition();

        // Offset
        Vector3 target = playerPos + new Vector3(0, 0, ZOffset);

        // Seguir suavemente
        Vector3 current = GameObject.Transform.Position;
        Vector3 smooth = Vector3.Lerp(current, target, FollowSpeed * deltaTime);
        GameObject.Transform.Position = smooth;

        // ===== Rotación manual =====
        Vector3 dir = playerPos - smooth;
        dir.Normalize();

        float yaw = MathF.Atan2(dir.X, dir.Z); // rotación Y

        GameObject.Transform.SetRotation(new Vector3(0, yaw, 0));
    }
}
