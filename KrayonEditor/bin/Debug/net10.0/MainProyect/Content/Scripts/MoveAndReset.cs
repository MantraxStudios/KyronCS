using KrayonCore;
using OpenTK.Mathematics;

public class MoveAndReset : KrayonBehaviour
{
    public float _MoveSpeed = 15.0f; 

    public override void Update(float deltaTime)
    {
        Quaternion currentRot = GameObject.Transform.GetWorldRotation();

        float angleRad = MathHelper.DegreesToRadians(_MoveSpeed * deltaTime);
        Quaternion deltaRot = Quaternion.FromAxisAngle(Vector3.UnitY, angleRad);

        Quaternion newRot = deltaRot * currentRot;

        GameObject.Transform.SetWorldRotation(newRot);
    }
}
