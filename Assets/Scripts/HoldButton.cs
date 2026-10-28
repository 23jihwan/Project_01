using UnityEngine;
using UnityEngine.EventSystems;

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Transform player;
    public Vector2 direction = Vector2.left;
    public float speed = 5f;
    private bool held;

    public void OnPointerDown(PointerEventData e) { held = true; }
    public void OnPointerUp(PointerEventData e) { held = false; }

    void Update()
    {
        if (held && player != null) player.Translate(direction * speed * Time.deltaTime);
    }
}