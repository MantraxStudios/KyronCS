using KrayonCore;
using KrayonCore.Components.Components;
using OpenTK.Mathematics;

public class FollowPlayer : KrayonBehaviour
{
    public GameObject _Player;
    public float MoveSpeed = 5f;

    public override void Update(float deltaTime)
    {
        Vector3 _G = _Player.Transform.Position;
        _G.X += MoveSpeed * deltaTime;
        _Player.Transform.SetPosition(_G);
    }
}
