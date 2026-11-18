using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum State { Idle, Hide, Pursue, Evade, Wander, Complex }

public enum ComplexState { Wandering, Approaching, Hiding }

public class Bot : MonoBehaviour
{
    public NavMeshAgent agent;
    public GameObject target; //target sera el cop que lo pondremos en Unity
    
    public State currentState = State.Idle;

    [Header("Flee")]
    public float fleeDistance = 10f;

    [Header("Pursue / Evade")]
    public float maxPredictionTime = 2f;

    Vector3 lastTargetPos;
    Vector3 targetVelocity;

    [Header("Wander")]
    public float wanderRadius = 5f;
    public float wanderDistance = 8f;
    public float wanderJitter = 1f;

    [Header("Hide")]
    public float hideMargin = 1.5f;

    [Header("Percepcion")]
    public float visionAngle = 60f;
    public float detectionRange = 15f;
    public LayerMask obstacleMask = ~0;  


    GameObject currentHideSpot;

    [Header("Complex")]
    public ComplexState complexState = ComplexState.Wandering;
    public float hideTime = 4f;              // cuánto se "lo piensa" antes de volver
    public float exitRangeMultiplier = 1.3f; // histéresis de distancia
    public float spotSwitchBias = 2f;        // histéresis de escondite

    float lastSeenTime = -999f;

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lastTargetPos = target.transform.position;
    }

    void Seek(Vector3 location)
    {
        agent.SetDestination(location);
    }
    //En seek ponemos como destino la posicion del target, en Flee (huir) ponemos como
    //destino la posicion del target en negativo para que vaya huyendo de esa posicion
    void Flee(Vector3 location)
    {
       Vector3 fleeDir = (transform.position - location).normalized;
       Vector3 fleeTarget = transform.position + fleeDir * fleeDistance;

       if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void Pursue() //P
    {
        Vector3 future = PredictTargetPosition();
        Debug.DrawLine(target.transform.position, future, Color.magenta);
        Seek(future);
    }

    void Evade() //E
    {
        Vector3 future = PredictTargetPosition();
        Debug.DrawLine(target.transform.position, future, Color.magenta);
        Flee(future);
    }


	//Para Wander necesitamos una var. que recordemos en cada llamada y por lo
	//tanto no puede ser local al metodo.
    Vector3 wanderTarget = Vector3.zero; //Se actualiza cada vez que el agente
										 //tiene un nuevo valor en su posicion
    void Wander() //R
    {
        wanderTarget += new Vector3(Random.Range(-1f, 1f) * wanderJitter, 0f, Random.Range(-1f, 1f) * wanderJitter);

        wanderTarget = wanderTarget.normalized * wanderRadius;

        Vector3 targetLocal = wanderTarget + new Vector3(0f, 0f, wanderDistance);

        Vector3 targetWorld = transform.TransformPoint(targetLocal);

        if (NavMesh.SamplePosition(targetWorld, out NavMeshHit hit, wanderRadius + wanderDistance, NavMesh.AllAreas))
        {
            Debug.DrawLine(transform.position, hit.position, Color.yellow);
            Seek(hit.position);
        }
    }

    bool Hide() //H
    {
        GameObject[] spots = World.Instance.GetHidingSpots();

        float bestDist = Mathf.Infinity;
        Vector3 bestPos = Vector3.zero;
        GameObject bestSpot = null;

        foreach (GameObject spot in spots)
        {
            Collider col = spot.GetComponentInChildren<Collider>();
            if (col == null) continue;

            // Centro del obstáculo, pero a la altura del ladrón
            Vector3 center = col.bounds.center;
            center.y = transform.position.y;

            // 1) Dirección Cop -> obstáculo (solo en el plano)
            Vector3 hideDir = center - target.transform.position;
            hideDir.y = 0f;
            hideDir.Normalize();

            // 2) Punto muy por detrás del obstáculo
            float far = col.bounds.size.magnitude + 1f;
            Vector3 rayStart = center + hideDir * far;

            // 3) Rayo de vuelta: toca la cara trasera
            Ray backRay = new Ray(rayStart, -hideDir);
            if (!col.Raycast(backRay, out RaycastHit info, far * 2f)) continue;

            // 4) Un poco detrás de esa cara, y dentro del NavMesh
            Vector3 candidate = info.point + hideDir * hideMargin;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2f, NavMesh.AllAreas)) continue;

            float d = Vector3.Distance(transform.position, navHit.position);
            if (spot == currentHideSpot) d -= spotSwitchBias;   // b) preferir el escondite actual

            if (d < bestDist)
            {
                bestDist = d;
                bestPos = navHit.position;
                bestSpot = spot;
            }
        }

        currentHideSpot = bestSpot;
        if (bestSpot == null) return false;

        Debug.DrawLine(transform.position, bestPos, Color.blue);
        Seek(bestPos);
        return true;
    }

    void Complex() //C
    {
        switch (complexState)
        {
            case ComplexState.Wandering:
                Wander();
                if (IsTargetInRange() && CanSeeTarget())
                    ChangeComplexState(ComplexState.Approaching);
                break;

            case ComplexState.Approaching:
                Pursue();
                if (CanTargetSeeMe())
                {
                    lastSeenTime = Time.time;
                    ChangeComplexState(ComplexState.Hiding);
                }
                else if (!IsTargetInRange(exitRangeMultiplier))
                    ChangeComplexState(ComplexState.Wandering);
                break;

            case ComplexState.Hiding:
                if (CanTargetSeeMe())
                    lastSeenTime = Time.time;     // me sigue viendo: reinicio la memoria

                if (!Hide())
                    Evade();                      // sin escondite posible: huyo

                if (Time.time - lastSeenTime > hideTime)
                    ChangeComplexState(IsTargetInRange() ? ComplexState.Approaching
                                                        : ComplexState.Wandering);
                break;
        }
    }

    void ChangeComplexState(ComplexState newState)
    {
        if (complexState == newState) return;
        Debug.Log($"{name} [Complex]: {complexState} → {newState}");
        complexState = newState;
    }


    bool CanSeeTarget()
    {
        return HasLineOfSight(transform.position, target.transform.position);
    }

    //En el siguiente comportamiento el robber se acerca al cop mientras este no le mira pero, cuando esto sucede, el robber se esconde.
    //Tenemos que decidir cuando el cop ve al rubber, digamos que esto sucede si esta en su angulo de vision (p.e. 60�)
    bool CanTargetSeeMe()
    {
        Vector3 copPos = target.transform.position;
        Vector3 toMe = transform.position - copPos;

        if (Vector3.Angle(target.transform.forward, toMe) > visionAngle * 0.5f)
            return false;

        return HasLineOfSight(copPos, transform.position);
    }

    bool IsTargetInRange(float multiplier = 1f)
    {
        return Vector3.Distance(transform.position, target.transform.position) < detectionRange * multiplier;
    }

    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        bool blocked = Physics.Linecast(from, to, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore);
        Debug.DrawLine(from, blocked ? hit.point : to, blocked ? Color.green : Color.red);
        return !blocked;
    }


    // Update is called once per frame
    void Update()
    {
        UpdateTargetVelocity();
        ReadInput();

        switch (currentState)
        {
            case State.Hide:    Hide();    break;
            case State.Pursue:  Pursue();  break;
            case State.Evade:   Evade();   break;
            case State.Wander:  Wander();  break;
            case State.Complex: Complex(); break;
            case State.Idle:    break;
        }
    }

    void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.H)) ChangeState(State.Hide);
        if (Input.GetKeyDown(KeyCode.P)) ChangeState(State.Pursue);
        if (Input.GetKeyDown(KeyCode.E)) ChangeState(State.Evade);
        if (Input.GetKeyDown(KeyCode.R)) ChangeState(State.Wander);
        if (Input.GetKeyDown(KeyCode.C)) ChangeState(State.Complex);
    }

    void ChangeState(State newState)
    {
        if (currentState == newState) return;
        Debug.Log($"{name}: {currentState} → {newState}");
        currentState = newState;

        if (newState == State.Complex)
            complexState = ComplexState.Wandering;
        }

    void UpdateTargetVelocity()
    {
        Vector3 current = target.transform.position;
        if (Time.deltaTime > 0f) targetVelocity = (current - lastTargetPos) / Time.deltaTime;
        lastTargetPos = current;
    }

    Vector3 PredictTargetPosition()
    {
        Vector3 targetPos = target.transform.position;
        float targetSpeed = targetVelocity.magnitude;

        if (targetSpeed < 0.01f)
            return targetPos;

        float distance = Vector3.Distance(transform.position, targetPos);
        float lookAhead = distance / (agent.speed + targetSpeed);
        lookAhead = Mathf.Min(lookAhead, maxPredictionTime);

        return targetPos + targetVelocity * lookAhead;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (target == null) return;
        Vector3 copPos = target.transform.position;
        Vector3 left  = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * target.transform.forward;
        Vector3 right = Quaternion.Euler(0,  visionAngle * 0.5f, 0) * target.transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(copPos, left * detectionRange);
        Gizmos.DrawRay(copPos, right * detectionRange);

        Gizmos.color = Color.yellow;
        Vector3 circleCenter = transform.TransformPoint(new Vector3(0f, 0f, wanderDistance));
        Gizmos.DrawWireSphere(circleCenter, wanderRadius);
        Gizmos.DrawSphere(transform.TransformPoint(wanderTarget + new Vector3(0f, 0f, wanderDistance)), 0.3f);
    }
}