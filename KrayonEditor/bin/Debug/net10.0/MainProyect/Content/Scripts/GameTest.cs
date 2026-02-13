using KrayonCore;
using KrayonCore.Core.Input;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class GameTest : KrayonBehaviour
{
    private Vector3 center;     // punto alrededor del cual gira
    private float angle = 0f;   // ángulo acumulado
    private float radius = 5f;  // distancia al centro
    private float speed = 1f;   // velocidad de giro

    public override void Start()
    {
        center = GameObject.Transform.GetWorldPosition();
    }

    public override void Update(float deltaTime)
    {
        angle += speed * deltaTime;

        float x = center.X + MathF.Cos(angle) * radius;
        float z = center.Z + MathF.Sin(angle) * radius;

        Vector3 newPos = new Vector3(x, center.Y, z);
        GameObject.Transform.SetWorldPosition(newPos);
    }
}
