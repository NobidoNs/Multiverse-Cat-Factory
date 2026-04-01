using UnityEngine;

public class Conveyor : MonoBehaviour
{
    public float speed = 5f;
    public Material mt;
    public float textureScrollSpeed = 1f;
    
    void FixedUpdate()
    {
        // Анимация текстуры
        if (mt != null)
        {
            float offset = Time.time * textureScrollSpeed;
            mt.mainTextureOffset = new Vector2(offset, 0f);
        }
    }
    
    void OnCollisionStay(Collision collision)
    {
        // Толкаем объекты, которые касаются конвейера
        Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 pushDirection = transform.forward * speed;
            rb.MovePosition(rb.position + pushDirection * Time.fixedDeltaTime);
        }
    }
}