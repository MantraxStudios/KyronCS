using KrayonCore;
using OpenTK.Mathematics;

public class JumpOnTrigger : KrayonBehaviour
{
    public override void OnCollisionEnter(GameObject other)
    {
        if (other.Tag == "Player")
        {
            other.GetComponent<Rigidbody>().AddForce(new Vector3(0, 5.0f, 0));
        }
    }
}
