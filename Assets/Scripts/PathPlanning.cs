using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

public class PathPlanning : MonoBehaviour
{
    [SerializeField]
    public Transform pivotPoint;
    public DogInterface dog_interface;
    private NavMeshPath path;
    private const float ang_vel = .2f; 
    private const float lin_vel = .2f; 
    private const float threshold = 0.1f;
    private bool person_found = false;
    private bool only_sim = false;

    private void Start()
    {
        Application.targetFrameRate = 20;
        path = new NavMeshPath();
        StartCoroutine(FollowPath());
        StartCoroutine(VisualizePath());
    }

    private IEnumerator SearchState()
    {
        dog_interface.Speak("Entering Search State");
        float targetAngleDegrees = 720;
        float currentAngleDegrees = 0;

        while (currentAngleDegrees < targetAngleDegrees)
        {
            float step = ang_vel * Mathf.Rad2Deg * Time.deltaTime; 
            transform.RotateAround(pivotPoint.position, Vector3.up, step); 
            currentAngleDegrees += step; 
            SendToDog(() => 
            {
                if (dog_interface.DetectPerson())
                {
                    person_found = true; 
                    dog_interface.Speak("Human Detected");
                    dog_interface.SendAngularVelocity(0); 
                }
                else
                {
                    dog_interface.SendAngularVelocity(-ang_vel);
                }
            });
            if (person_found) 
            {
                yield break;
            }
            yield return null; 
        }
        dog_interface.SendAngularVelocity(0); 
    }

    private IEnumerator FollowPath()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("NavTarget");

        foreach (GameObject target in targets)
        {
            yield return StartCoroutine(FollowToTarget(target));
            yield return StartCoroutine(SearchAndHandleTarget(target));
        }

        Application.Quit();
    }

    private IEnumerator FollowToTarget(GameObject target)
    {
        dog_interface.Speak("Going To Next Target");
        NavMesh.CalculatePath(transform.position, target.transform.position, NavMesh.AllAreas, path);
        while (path.corners.Length > 1)
        {
            NavMesh.CalculatePath(transform.position, target.transform.position, NavMesh.AllAreas, path);

            if (path.corners.Length > 1)
            {
                yield return StartCoroutine(GoToNextPoint(path.corners[0], path.corners[1], target));
            }
        }
    }

    private IEnumerator SearchAndHandleTarget(GameObject target)
    {
        person_found = false;
        yield return StartCoroutine(SearchState());
        if (person_found)
        {
            SendToDog(() => 
            {
                dog_interface.Speak("Starting Activity Recognition");
            });
            yield return new WaitForSeconds(30f); // Start Activity Recognition for 30s
        }
    }

    private IEnumerator GoToNextPoint(Vector3 start, Vector3 end, GameObject target)
    {
        yield return StartCoroutine(RotateTowards(start, end));
        NavMesh.CalculatePath(transform.position, target.transform.position, NavMesh.AllAreas, path);
        yield return StartCoroutine(RotateTowards(transform.position, end));
        yield return StartCoroutine(MoveTowards  (start, end));
    }

    private IEnumerator RotateTowards(Vector3 start, Vector3 end)
    {
        Vector3 directionToTarget = (end - start).normalized;
        float targetAngleDegrees =  Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
        float currentAngleDegrees = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
        
        while (Mathf.Abs(Mathf.DeltaAngle(currentAngleDegrees, targetAngleDegrees)) > threshold)
        {
            float step = ang_vel * Mathf.Rad2Deg * Time.deltaTime;
            float angleDifference = Mathf.DeltaAngle(currentAngleDegrees, targetAngleDegrees);
            float angleToRotate = Mathf.Sign(angleDifference) * Mathf.Min(Mathf.Abs(angleDifference), step);
            transform.RotateAround(pivotPoint.position, Vector3.up, angleToRotate);
            currentAngleDegrees += angleToRotate;
            SendToDog(() => 
            {
                if (angleDifference > 0)
                    dog_interface.SendAngularVelocity(-ang_vel);
                else
                    dog_interface.SendAngularVelocity(ang_vel);
            });
            yield return null;
        }
        dog_interface.SendAngularVelocity(0);        
    }

    private IEnumerator MoveTowards(Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);
        while (Vector3.Distance(transform.position, end) > threshold)
        {
            transform.position = Vector3.MoveTowards(transform.position, end, lin_vel * Time.deltaTime);
            SendToDog(() => 
            {
                dog_interface.SendLinearVelocity(lin_vel);
            });
            yield return null;
        }
        dog_interface.SendLinearVelocity(0);
        transform.position = end;
    }

    private IEnumerator VisualizePath()
    {
        while (true)
        {
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Debug.DrawLine(path.corners[i], path.corners[i + 1], Color.red, 0.1f);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void SendToDog(Action dogAction)
    {
        if (!only_sim)
            dogAction?.Invoke();
    }
}
