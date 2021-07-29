using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// basic setup as in https://github.com/SebLague/Ray-Marching
public class Prim : MonoBehaviour{
    
    // list of currently renderable Primitives
    public enum PrimType {Sphere, GroundPlane, Box, Torus};

    // list of combination Modes
    public enum CombinationMode {Add, Subtract, Intersect, smoothMin};

    // public Primitive Attributes
    public PrimType primType;
    public CombinationMode combinationMode;
    public int EvaluationOrder;
    // Slider Smooth Min amount
    [Range(0,10)]
    public float smoothAmount;
    public Color color = Color.white;

    [Range(0,1)]
    public float diffuse = 1;
    [Range(0,1)]
    public float specular = 0;
    public int specularHardness = 64; 

    // set position of Primitive based on Gameobject position
    public Vector3 Position{
        get {
            return transform.position;
        }
    }

    // set cale of Primitive based on Gameobject scale
    public Vector3 Scale{
        get {
            return transform.localScale;
        }
    }

    // convert Quarternion rotation to Euler
    public Vector3 Rotation{
        get {
            return transform.localRotation.eulerAngles;
        }
    }
}
