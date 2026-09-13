using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(humanoidMotor))]
[RequireComponent(typeof(NavMeshAgent))]
public class BotController : MonoBehaviour
{
    [Header("AI Settings")]
    public Transform target;
    public float rotationSpeed = 10f;
    public float stoppingDistance = 1.5f;

    private humanoidMotor motor;
    private NavMeshAgent agent;

    private void Start()
    {
        motor = GetComponent<humanoidMotor>();
        agent = GetComponent<NavMeshAgent>();

        // Disable the agent's native transforms so the humanoidMotor's Rigidbody retains full physical control[cite: 14]
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.stoppingDistance = stoppingDistance;
    }

    private void Update()
    {
        if (target == null)
        {
            motor.SetMoveInput(Vector2.zero);
            return;
        }

        // Keep the invisible agent tethered to the physical body[cite: 14]
        agent.nextPosition = transform.position;
        agent.SetDestination(target.position);

        Vector3 desiredVelocity = agent.desiredVelocity;

        // 1. Calculate Movement
        if (agent.remainingDistance > agent.stoppingDistance)
        {
            // Convert the world-space path into localized X/Y input axes (-1 to 1) to pilot the motor
            Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : transform;
            Vector3 localDesired = refTransform.InverseTransformDirection(desiredVelocity).normalized;

            motor.SetMoveInput(new Vector2(localDesired.x, localDesired.z));
        }
        else
        {
            motor.SetMoveInput(Vector2.zero);
        }

        // 2. Calculate Rotation
        if (desiredVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity.normalized);
            float yAngleDelta = Mathf.DeltaAngle(transform.eulerAngles.y, targetRotation.eulerAngles.y);
            float rotationStep = yAngleDelta * rotationSpeed * Time.deltaTime;

            motor.Rotate(new Vector3(0f, rotationStep, 0f));
        }
    }
}