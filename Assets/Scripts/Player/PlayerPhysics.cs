﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof(Rigidbody))]
public class PlayerPhysics : MonoBehaviour {

    public enum PlayerPhysicsState
    {
        Paused,
        RegularMoving,
        Accelerating,
        Dropping,
        Gliding,
        Dead,
        AutoPilot
    }

    public float pushForce;
    public float accelerateForce;
    public float launchForce = 100;
    //public float accelarateTime;
    public float gravity;
    public float m_CrashWallDontDieTime;

    public float minRotateSpeed;
    public float maxRotateSpeed;
    public float minBoostRotateSpeed;
    public float maxBoostRotateSpeed;
    public float timeToReachMaxRotateSpeed;
    public float timeToReachMaxBoost;

    public float MinHoverDistance = 2.0f;
    public float MaxHoverDistance = 4.0f;
    public float ResetYHoverDistance = 0.5f;
    public float MaxHoverForce = 25.0f;
    public float GravityDetectRadius = 2.0f;
    float rotateSpeed;
    float rotateTime;
    float boostTime;
    float afterBoostTime;
    bool rotating;
    
    public float force;

    float m_SoundSpeed = 0;
    const float m_SoundSpeedUpRate = 100.0f;
    const float m_SoundSpeedDownRate = 200.0f;

    Rigidbody m_RigidBody;

    [HideInInspector]
    public PlayerPhysicsState playerPhysicsState;
    [HideInInspector]
    public int collisionCount;


    void Start () {
        playerPhysicsState = PlayerPhysicsState.Paused;
        m_RigidBody = GetComponent<Rigidbody>();
        afterBoostTime = 0;
        m_SoundSpeed = 0;
    }
	
	void Update () {
        if(AudioSystem.current)
        {
            AudioSystem.current.SetSpeed(m_SoundSpeed);
        }
    }

    public bool CanBeKilledByWall()
    {
        return !(playerPhysicsState == PlayerPhysicsState.Accelerating || afterBoostTime < m_CrashWallDontDieTime);
    }

    public void SetPhysicsState(PlayerPhysicsState state)
    {
        playerPhysicsState = state;
        if (playerPhysicsState == PlayerPhysicsState.RegularMoving)
        {
            m_RigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            GetComponent<Rigidbody>().useGravity = true;
            GetComponent<Rigidbody>().isKinematic = false;
        }
        else if (playerPhysicsState == PlayerPhysicsState.Accelerating)
        {

            GetComponent<Rigidbody>().isKinematic = false;
        }
        else if (playerPhysicsState == PlayerPhysicsState.Dropping)
        {
            GetComponent<Rigidbody>().useGravity = true;
            GetComponent<Rigidbody>().isKinematic = false;
        }
        else if (playerPhysicsState == PlayerPhysicsState.Dead)
        {
            m_RigidBody.constraints = 0;
            m_RigidBody.AddTorque(new Vector3(1, 1, 1), ForceMode.Impulse);
            GetComponent<Rigidbody>().isKinematic = false;
        }
        else if (playerPhysicsState == PlayerPhysicsState.AutoPilot)
        {
            m_RigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            GetComponent<Rigidbody>().useGravity = true;
            GetComponent<Rigidbody>().isKinematic = true;
        } 
        else if(playerPhysicsState == PlayerPhysicsState.Gliding)
        {
            GetComponent<Rigidbody>().useGravity = true;
        }
    }

    public void ApplyForce(Vector3 force)
    {
        m_RigidBody.AddForce(force);
    }

    public void Launch()
    {
        m_RigidBody.AddForce(transform.localToWorldMatrix * Vector3.forward * launchForce, ForceMode.Impulse);
    }

    void FixedUpdate()
    {
        
        if (!rotating)
        {
            rotateTime = 0;
        }
        else {
            rotating = false;
        }
        rotateTime += Time.fixedDeltaTime;

        if (playerPhysicsState == PlayerPhysicsState.RegularMoving)
        {
            boostTime = 0;
            afterBoostTime += Time.fixedDeltaTime;
            m_RigidBody.AddForce(transform.forward * pushForce);

            //ApplyHover();
            ApplyGravityIfOffTrack();

            if (m_SoundSpeed > 0)
            {
                m_SoundSpeed -= m_SoundSpeedDownRate * Time.fixedDeltaTime;
                m_SoundSpeed = Mathf.Clamp(m_SoundSpeed, 0.0f, 100.0f);
            }
        }
        else if (playerPhysicsState == PlayerPhysicsState.Accelerating)
        {
            boostTime += Time.fixedDeltaTime;
            afterBoostTime = 0;
            m_RigidBody.AddForce(transform.forward * accelerateForce);
            //ApplyHover();
            ApplyGravityIfOffTrack();

            if (m_SoundSpeed < 100.0f)
            {
                m_SoundSpeed += m_SoundSpeedUpRate * Time.fixedDeltaTime;
                m_SoundSpeed = Mathf.Clamp(m_SoundSpeed, 0.0f, 100.0f);
            }
        }
        else if (playerPhysicsState == PlayerPhysicsState.Dropping)
        {
            boostTime = 0;
            if (collisionCount == 0)
                m_RigidBody.AddForce(Vector3.down * gravity * m_RigidBody.mass);
            //ApplyGravityIfOffTrack();
        }
        else if(playerPhysicsState == PlayerPhysicsState.Gliding)
        {
            boostTime += Time.fixedDeltaTime;
            Vector3 forward = transform.forward;
            float force = pushForce;
            if (InputHandler.current.m_Accelerate)
                force = accelerateForce;
            forward.y = 0;
            forward.Normalize();
            forward *= 0.95f;
            forward.y = 0.05f;
            m_RigidBody.AddForce(transform.forward * force); //(transform.localToWorldMatrix * Vector3.forward * pushForce);
        }
    }

    void ApplyHover()
    {
        RaycastHit hit;
        Ray ray = new Ray(transform.position, -Vector3.up);

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            //print("Found an object - distance: " + hit.distance);
            float distance = hit.distance;
            if(distance <= ResetYHoverDistance || distance >= MaxHoverDistance)
            {
                Vector3 speed = gameObject.GetComponent<Rigidbody>().velocity;
                if((speed.y <= 0 && distance <= ResetYHoverDistance) || (speed.y > 0 && distance >= MaxHoverDistance))
                {
                    speed.y = 0;
                    gameObject.GetComponent<Rigidbody>().velocity = speed;
                }
            }
            float fractionalPosition = (MaxHoverDistance - distance) / (MaxHoverDistance - MinHoverDistance);
            if (fractionalPosition < 0) fractionalPosition = 0;
            if (fractionalPosition > 1) fractionalPosition = 1;
            float force = fractionalPosition * MaxHoverForce;
            gameObject.GetComponent<Rigidbody>().AddForce(Vector3.up * force);
        }
    }

    void ApplyGravityIfOffTrack()
    {
        Vector3 p1 = transform.position;

        /*Collider[] hitColliders = Physics.OverlapSphere(transform.position, GravityDetectRadius);
        if(hitColliders.Length == 1)
        {
            //print("Gravirty");
            m_RigidBody.AddForce(Vector3.down * gravity * m_RigidBody.mass);
        }*/

        RaycastHit[] hits;
        float distanceToObstacle = 0;

        // Cast a sphere wrapping character controller 10 meters forward
        // to see if it is about to hit anything.
        hits = Physics.SphereCastAll(p1, GravityDetectRadius, -Vector3.up, 0.1f);
        int floorCount = 0;
        foreach(var hit in hits)
        {
            if (hit.transform.CompareTag("Floor"))
                floorCount++;
        }
        if (floorCount == 0)
        {
            m_RigidBody.AddForce(Vector3.down * gravity * m_RigidBody.mass);
        }
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(0, 15, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = new Color(0.0f, 0.0f, 0.5f, 1.0f);
        float speed = m_RigidBody.velocity.magnitude;
        string text = string.Format("Speed : {0:0.0}", speed);
        GUI.Label(rect, text, style);
    }

    void OnCollisionEnter(Collision collision)
    {
        collisionCount++;
    }

    void OnCollisionExit(Collision collision)
    {
        collisionCount--;
    }
	/*
	public float getAccelarateTime()
	{
		return accelarateTime;
	}
	*/
    public virtual void RegularPush()
    {
        playerPhysicsState = PlayerPhysicsState.RegularMoving;
    }

    public virtual void AcceleratePush()
    {
        playerPhysicsState = PlayerPhysicsState.Accelerating;
    }

    public virtual void RotateLeft(float m_RotatePercentage = 1.0f)
    {
        rotating = true;
        float boostPercentage = boostTime / timeToReachMaxBoost;
        float minRotSpeed = Mathf.Lerp(minRotateSpeed * m_RotatePercentage, minBoostRotateSpeed * m_RotatePercentage, boostPercentage);
        float maxRotSpeed = Mathf.Lerp(maxRotateSpeed * m_RotatePercentage, maxBoostRotateSpeed * m_RotatePercentage, boostPercentage);

        rotateSpeed = Mathf.Lerp(minRotSpeed, maxRotSpeed, rotateTime / timeToReachMaxRotateSpeed);
        transform.Rotate(transform.worldToLocalMatrix * Vector3.up, -rotateSpeed * Time.fixedDeltaTime);
    }

    public virtual void RotateRight(float m_RotatePercentage = 1.0f)
    {
        rotating = true;
        float boostPercentage = boostTime / timeToReachMaxBoost;
        float minRotSpeed = Mathf.Lerp(minRotateSpeed * m_RotatePercentage, minBoostRotateSpeed * m_RotatePercentage, boostPercentage);
        float maxRotSpeed = Mathf.Lerp(maxRotateSpeed * m_RotatePercentage, maxBoostRotateSpeed * m_RotatePercentage, boostPercentage);

        rotateSpeed = Mathf.Lerp(minRotSpeed, maxRotSpeed, rotateTime / timeToReachMaxRotateSpeed);
        transform.Rotate(transform.worldToLocalMatrix * Vector3.up, rotateSpeed * Time.fixedDeltaTime);
    }
		
}