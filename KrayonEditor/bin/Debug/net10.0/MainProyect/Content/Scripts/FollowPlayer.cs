using KrayonCore;
using OpenTK.Mathematics;
using KrayonCore.Components;

public class FollowPlayer : KrayonBehaviour
{
    public GameObject _Player;
    public float MoveSpeed = 5f;

    public override void Update(float deltaTime)
    {
        Vector3 direction = _Player.Transform.GetWorldPosition();
        direction.X += MoveSpeed * deltaTime;
        _Player.Transform.SetWorldPosition(direction); 
    }
}
