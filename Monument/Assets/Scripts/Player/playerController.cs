using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(humanoidMotor))]
public class playerController : MonoBehaviour
{
    [Header("Look Sensitivities")]
    [SerializeField] private float mouseSens = 10f;
    [SerializeField] private float mouseSensMultiplier = 0.01f;
    [SerializeField] private float joystickLookSens = 150f;

    [Header("Interaction & Weapons")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private float interactRadius = 0.25f;
    [SerializeField] private LayerMask interactMask;
    public Transform weaponHolder;
    public Weapon currentWeapon { get; set; }
    public Camera playerCamera;

    [Header("Input Setup")]
    [SerializeField] private KeybindFramework input;

    public humanoidMotor motor { get; private set; }
    public bool hasMoveInput { get; private set; }
    public bool inVehicle { get; private set; } = false;

    private bool ignoreNextDelta = false;
    [HideInInspector] public PlayerVehicleController vehicleController;

    private void OnEnable()
    {
        ignoreNextDelta = true;
    }

    private void OnDisable()
    {
        if (motor != null)
        {
            motor.SetMoveInput(Vector2.zero);
            motor.SetSprintInput(false);
            motor.Rotate(Vector3.zero);
            motor.RotateCamera(0f);
        }
    }

    private void Start()
    {
        motor = GetComponent<humanoidMotor>();
        vehicleController = GetComponent<PlayerVehicleController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    // --- VEHICLE HANDOFF MECHANICS ---
    public void DisableForVehicle(Vehicle vehicle)
    {
        inVehicle = true;
        motor.enabled = false;

        if (vehicleController != null)
        {
            vehicleController.activeVehicle = vehicle;
            vehicleController.enabled = true;
        }

        Rigidbody rb = motor.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        CapsuleCollider col = motor.GetComponent<CapsuleCollider>();
        if (col != null) col.enabled = false;
    }

    public void EnableFromVehicle()
    {
        inVehicle = false;
        motor.enabled = true;

        if (vehicleController != null)
        {
            vehicleController.activeVehicle = null;
            vehicleController.enabled = false;
        }

        Rigidbody rb = motor.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        CapsuleCollider col = motor.GetComponent<CapsuleCollider>();
        if (col != null) col.enabled = true;
    }

    private void Update()
    {
        if (inVehicle) return;

        if (input != null)
        {
            // --- Movement Input Routing ---
            float _xMov = 0f;
            float _zMov = 0f;

            if (input.strafeRight.IsPressed()) _xMov += 1f;
            if (input.strafeLeft.IsPressed()) _xMov -= 1f;
            if (input.walkForwards.IsPressed()) _zMov += 1f;
            if (input.walkBackwards.IsPressed()) _zMov -= 1f;

            Vector2 axisInput = input.walkAxis.ReadValue<Vector2>();
            if (axisInput != Vector2.zero)
            {
                _xMov = axisInput.x;
                _zMov = axisInput.y;
            }

            hasMoveInput = Mathf.Abs(_xMov) > 0.05f || Mathf.Abs(_zMov) > 0.05f;

            motor.SetMoveInput(new Vector2(_xMov, _zMov));
            motor.SetSprintInput(input.sprint.IsPressed());
            motor.SetJumpHeld(input.jump.IsPressed());

            // --- Toggle Triggers ---
            if (input.crouch.triggered) motor.ToggleCrouch();
            if (input.prone.triggered) motor.ToggleProne();
            if (input.jump.triggered) motor.Jump();

            // --- Interaction ---
            if (input.interact.triggered)
            {
                PerformInteract();
            }

            // --- Weapon Interaction ---
            if (currentWeapon != null)
            {
                bool isMovingProne = motor.currentStance == humanoidMotor.PlayerStance.Proning && hasMoveInput;
                bool canShoot = !motor.isSprinting && !isMovingProne;

                currentWeapon.ProcessInput(input.fire.IsPressed() && canShoot, input.fire.triggered && canShoot);

                if (input.reload.triggered) currentWeapon.Reload();
                if (input.drop.triggered) currentWeapon.Drop();
            }
        }

        // --- Hybrid Rotation Calculation ---
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        if (ignoreNextDelta)
        {
            mouseDelta = Vector2.zero;
            ignoreNextDelta = false;
        }

        Vector2 joystickLook = input != null && input.lookAxis != null ? input.lookAxis.ReadValue<Vector2>() : Vector2.zero;

        float mouseYaw = mouseDelta.x * mouseSensMultiplier * mouseSens;
        float joystickYaw = joystickLook.x * joystickLookSens * Time.deltaTime;
        motor.Rotate(new Vector3(0f, mouseYaw + joystickYaw, 0f));

        float mousePitch = -mouseDelta.y * mouseSensMultiplier * mouseSens;
        float joystickPitch = -joystickLook.y * joystickLookSens * Time.deltaTime;
        motor.RotateCamera(mousePitch + joystickPitch);
    }

    private void PerformInteract()
    {
        if (motor == null || motor.cameraHolder == null) return;

        if (Physics.SphereCast(motor.cameraHolder.position, interactRadius, motor.cameraHolder.forward, out RaycastHit hit, interactRange, interactMask))
        {
            Vehicle vehicle = hit.collider.GetComponentInParent<Vehicle>();
            if (vehicle != null && !vehicle.isOccupied)
            {
                vehicle.EnterVehicle(this);
                return;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(this.gameObject);
            }
        }
    }
}