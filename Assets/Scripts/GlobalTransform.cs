using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GlobalTransform : MonoBehaviour
{
    public Vector3 p, r, s, r_right;
    public Quaternion q, q_right, q_local, q_local_test;
    void Update()
    {
        p = transform.position;
        r = transform.eulerAngles;
        s = transform.lossyScale;
        q = transform.rotation;

        q_right = transform.rotation;
        q_right.x *= -1.0f;
        q_right.w *= -1.0f;
        r_right = transform.eulerAngles;
        r_right.y *= -1.0f;
        r_right.z *= -1.0f;

        q_local = transform.localRotation;
        q_local_test = Quaternion.Inverse(transform.parent.rotation) * transform.rotation;
    }
}
