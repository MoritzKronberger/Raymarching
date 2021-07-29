using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// basic setup as in https://github.com/SebLague/Ray-Marching
public class RaymarchingSetup : MonoBehaviour
{   
    // Compute Shader to process in
    public ComputeShader computeShader;

    // target framerate to reach
    public int targetFPS = 60;

    [Range(0,10)]
    public double ambientLightIntensity = 0.1;
    public Color backgroundColor = Color.black;

    // render Texture to display
    RenderTexture renderTexture;

    // compute shader kernel to use (currently 0)
    int kernelNum;

    // camera-gameobject to use
    Camera cam;

    // Light-game object to use
    Light pointLight;

    // list of ComputeBuffers to dispatch
    List<ComputeBuffer> activeBuffers;


    // Start is called before the first frame update
    void Start(){
        SetKernelNum("CSMain");

        // deactivate vSync and limit Framerate to limit GPU load on low effort tasks
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
    }

    // get "Hyperobjects" like camera
    void Init(){
        cam = Camera.current;
    }

    // call on each frame
    void OnRenderImage(RenderTexture source, RenderTexture destination){

        Init();

        activeBuffers = new List<ComputeBuffer>();

        // check if RenderTexture needs to be initialized or updated
        if(renderTexture == null || renderTexture.width != cam.pixelWidth || renderTexture.height != cam.pixelHeight){
            InitRenderTexture();
        }

        if(Application.targetFrameRate != targetFPS){
            Application.targetFrameRate = targetFPS;
        }

        // pass current state of scene to ComputeShader
        UpdateScene();
        SetSceneParameters();

        // set RenderTexture as Texture for ComputeShader to write to
    	computeShader.SetTexture(kernelNum, "Result", renderTexture);

        // pass current resolution to ComputeShader
        Vector2 resolution = new Vector2(renderTexture.width, renderTexture.height);
        computeShader.SetVector("Resolution", resolution);
        computeShader.Dispatch(kernelNum, renderTexture.width/8, renderTexture.height/8, 1);

        // bind RenderTexture to camera
        Graphics.Blit(renderTexture, destination);

        foreach (var buffer in activeBuffers){
            buffer.Dispose();
        }
    }

    // pass current state of Primitives and Lights to ComputeShader via primBuffer
    void UpdateScene(){

        // collect Scene Primitives and send Primitive-Data to ComputeShader
        List<Prim> scenePrims = new List<Prim> (FindObjectsOfType<Prim> ());
        scenePrims.Sort((a, b) => a.EvaluationOrder.CompareTo(b.EvaluationOrder));

        PrimAttribs[] primAttribs = new PrimAttribs[scenePrims.Count];

        for(int i = 0; i < scenePrims.Count; i++){
            var prim = scenePrims[i];

            // convert Unity-Color to Vector 3
            Vector3 color = new Vector3(prim.color.r, prim.color.g, prim.color.b);

            primAttribs[i] = new PrimAttribs{
                primType = (int) prim.primType,
                combinationMode = (int) prim.combinationMode,
                smoothAmount = prim.smoothAmount,
                color =          color,
                diffuse =       prim.diffuse,
                specular =      prim.specular,
                specularHardness = prim.specularHardness,
                position =       prim.Position,
                scale =          prim.Scale,
                rotation =      prim.Rotation 
            };
        } 

        // calculate size of data for ComputeBuffer
        int primTypeBites = sizeof(int);
        int combModeBites = sizeof(int);
        int smoothAmountBites = sizeof(float);
        int colorBites = sizeof(float)*3;
        int diffBites = sizeof(float);
        int specBites = sizeof(float);
        int specHBites = sizeof(int);
        int positionBites = sizeof(float)*3;
        int scaleBites = sizeof(float)*3;
        int rotateBits = sizeof(float)*3;

        int primAttribsSize = primTypeBites + combModeBites + smoothAmountBites + colorBites + diffBites + specBites + specHBites + positionBites + scaleBites + rotateBits;

        // add Primitive-Data into ComputeBuffer
        ComputeBuffer primBuffer = new ComputeBuffer(primAttribs.Length, primAttribsSize);

        // send Primitive-Data to ComputeShader
        primBuffer.SetData(primAttribs);
        computeShader.SetBuffer(0, "prims", primBuffer);
        computeShader.SetInt("primCount", primAttribs.Length);

        // dispatch ComputeBuffer
        activeBuffers.Add(primBuffer);



        // send updated Light Position to ComputeShader
        List<Light> sceneLights = new List<Light> (FindObjectsOfType<Light>());
        LightAttribs[] lightAttribs = new LightAttribs[sceneLights.Count];

        for(int i = 0; i < sceneLights.Count; i++){
            var light = sceneLights[i];

            Vector3 lColor = new Vector3(light.color.r, light.color.g, light.color.b);

            lightAttribs[i] = new LightAttribs{
                position   =   light.transform.position,
                brightness =   light.intensity * lColor
            };
        } 

        // calculate size of data for ComputeBuffer
        positionBites   = sizeof(float)*3;
        int brightnessBites = sizeof(float)*3;

        int lightAttribsSize = positionBites + brightnessBites;

        // add Light-Data into ComputeBuffer
        ComputeBuffer lightBuffer = new ComputeBuffer(lightAttribs.Length, lightAttribsSize);

        // send Primitive-Data to ComputeShader
        lightBuffer.SetData(lightAttribs);
        computeShader.SetBuffer(0, "lights", lightBuffer);
        computeShader.SetInt("lightCount", lightAttribs.Length);

        // dispatch ComputeBuffer
        activeBuffers.Add(lightBuffer);
    }

    // Primitive Attributes to be read by ComputeShader
    struct PrimAttribs{
        public int primType;
        public int combinationMode;
        public float smoothAmount;
        public Vector3 color;
        public float diffuse;
        public float specular;
        public int specularHardness;
        public Vector3 position;
        public Vector3 scale;
        public Vector3 rotation;
    }

    // Light Attributes to be read by ComputeShader
    struct LightAttribs{
        public Vector3 position;
        public Vector3 brightness;
    }

    // send scene parameters such as CameraToWorld-Matrix or inverse ProjectionMatrix to ComputeShader
    void SetSceneParameters(){
        computeShader.SetMatrix("CameraCoord_to_WorldCoord", cam.cameraToWorldMatrix);
        computeShader.SetMatrix("Inverse_Camera_Projection_Matrix", cam.projectionMatrix.inverse);
        // convert Unity-Color to Vector 3
        Vector3 bgColor = new Vector3(backgroundColor.r, backgroundColor.g, backgroundColor.b);
        computeShader.SetVector("backgroundColor", bgColor);
        computeShader.SetFloat("ambientLightIntensity", (float)ambientLightIntensity);
    }

    // initialize or update RenderTexture based on camera's resolution
    void InitRenderTexture(){
        if(renderTexture != null){
            renderTexture.Release();
        }
        renderTexture = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
    }

    // get ComputeShader kernel number by name
    void SetKernelNum(string kernelName){
        kernelNum = computeShader.FindKernel(kernelName);
    }
}
