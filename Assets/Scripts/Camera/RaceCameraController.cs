using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Free-look camera for professor: WASD movement, right-click mouse look,
/// Q/E altitude, scroll wheel speed. Uses unscaledDeltaTime to work while paused.
/// </summary>
public class RaceCameraController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base move speed (units/sec)")]
    public float MoveSpeed = 20f;

    [Tooltip("Minimum speed after scroll adjustment")]
    public float MinSpeed = 5f;

    [Tooltip("Maximum speed after scroll adjustment")]
    public float MaxSpeed = 100f;

    [Header("Look")]
    [Tooltip("Mouse look sensitivity")]
    public float LookSensitivity = 0.15f;

    private float currentSpeed;
    private float pitch;
    private float yaw;

    private void Start()
    {
        currentSpeed = MoveSpeed;
        Vector3 euler = transform.eulerAngles;
        pitch = euler.x;
        yaw = euler.y;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        float dt = Time.unscaledDeltaTime;

        // Mouse look (right-click held)
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * LookSensitivity;
            pitch -= mouseDelta.y * LookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // WASD + QE movement
        Vector3 move = Vector3.zero;
        if (Keyboard.current[Key.W].isPressed) move += transform.forward;
        if (Keyboard.current[Key.S].isPressed) move -= transform.forward;
        if (Keyboard.current[Key.A].isPressed) move -= transform.right;
        if (Keyboard.current[Key.D].isPressed) move += transform.right;
        if (Keyboard.current[Key.E].isPressed) move += Vector3.up;
        if (Keyboard.current[Key.Q].isPressed) move -= Vector3.up;

        transform.position += move.normalized * currentSpeed * dt;

        // Scroll wheel speed adjustment
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentSpeed = Mathf.Clamp(currentSpeed + scroll * 0.01f * currentSpeed, MinSpeed, MaxSpeed);
        }
    }
}
