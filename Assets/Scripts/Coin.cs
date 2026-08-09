using UnityEngine;

public class Coin : MonoBehaviour
{
    private float rotationSpeed = 200f;

    void Update()
    {
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
    }
}
