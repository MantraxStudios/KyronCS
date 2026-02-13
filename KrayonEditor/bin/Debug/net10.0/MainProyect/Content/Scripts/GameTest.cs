using KrayonCore;
using KrayonCore.Core.Input;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class GameTest : KrayonBehaviour
{
    public bool Colisiono;

    public override void Start()
    {
        
    }

    public override void Update(float deltaTime)
    {
        if (InputSystem.GetKeyReleased(Keys.G))
        {
            GameObject.GetComponent<Rigidbody>().AddForce(new Vector3(0.0f, 5.0f, 0.0f));
            Console.WriteLine("Hola");
        }
    }

    public override void OnCollisionEnter(GameObject other)
    {
        Colisiono = true;
    }

    public override void OnCollisionExit(GameObject other)
    {
        Colisiono = false;
    }
}