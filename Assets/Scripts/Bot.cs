using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum State { Idle, Hide, Pursue, Evade, Wander, Complex }

public class Bot : MonoBehaviour
{
    public NavMeshAgent agent;
    public GameObject target; //target sera el cop que lo pondremos en Unity
    
    public State currentState = State.Idle;

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Seek(Vector3 location)
    {
        //TODO
    }
    //En seek ponemos como destino la posicion del target, en Flee (huir) ponemos como
    //destino la posicion del target en negativo para que vaya huyendo de esa posicion
    void Flee(Vector3 location)
    {
       //TODO
    }

    void Pursue() //P
    {
        //TODO
    }

    //Evade es practicamente igual a Pursue solo que se debe ir en la direccion contraria
    void Evade() //E
    {
        //TODO
    }


	//Para Wander necesitamos una var. que recordemos en cada llamada y por lo
	//tanto no puede ser local al metodo.
    Vector3 wanderTarget = Vector3.zero; //Se actualiza cada vez que el agente
										 //tiene un nuevo valor en su posicion
    void Wander() //R
    {
        //TODO
    }

    void Hide() //H
    {
        //TODO
    }

    void CleverHide()
    {
        //TODO
    }

    void Complex()
    {
        
    }


    bool CanSeeTarget()
    {
        //TODO
        return false;
    }

    //En el siguiente comportamiento el robber se acerca al cop mientras este no le mira pero, cuando esto sucede, el robber se esconde.
    //Tenemos que decidir cuando el cop ve al rubber, digamos que esto sucede si esta en su angulo de vision (p.e. 60�)
    bool TargetCanSeeMe()
    {
        //TODO
        return false;
    }


    // Update is called once per frame
    void Update()
    {
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
    }
}