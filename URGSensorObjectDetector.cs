using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// http://sourceforge.net/p/urgnetwork/wiki/top_jp/
// https://www.hokuyo-aut.co.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf
public class URGSensorObjectDetector : MonoBehaviour
{


    public static URGSensorObjectDetector Instance = null;

    [Header("Connection with Sensor")]

    public string ip_address = "192.168.0.10";
    public int port_number = 10940;
    UrgDeviceEthernet urg;
    public int sensorScanSteps { get; private set; }
    public bool open { get; private set; }
    bool gd_loop = false;

    [Header("Detection Area Constrain")]
    public DistanceCroppingMethod distanceCroppingMethod = DistanceCroppingMethod.RADIUS;
    long[] distanceConstrainList;

    [Header("----------Rect based constrain")]
    public int detectRectWidth;     //Unit is MM
    public int detectRectHeight;    //Unit is MM
    public Rect detectAreaRect
    {
        get
        {
            Rect rect = new Rect(0, 0, detectRectWidth, detectRectHeight);
            rect.x -= (detectRectWidth / 2);
            return rect;
        }
    }

    [Header("----------Radius based constrain")]
    public long maxDetectionDist = 7000;//for radius based detection, unit is mm

    [Header("Post Processing Distance Data")]
    public bool smoothDistanceCurve;
    public bool smoothDistanceByTime;
    [Range(1, 130)] public int smoothKernelSize = 21;
    List<long> smoothByTimePreviousList = new List<long>();
    [Range(0.01f, 1f)] public float timeSmoothFactor;

    [Header("Object Detection")]
    public float deltaLimit = 100;
    [Range(1, 40)] public int noiseLimit = 7;
    List<Vector3> detectedDir;
    List<float> detectedDis;
    List<DetectObject> detectedObjects;

    [Header("Debug Draw")]

    public bool debugDraw = false;
    public bool debugDrawDistance = false;
    public bool drawObjectRays;
    public bool drawObjectCenterRay;
    public bool drawObject;

    //colors
    public Color distanceColor = Color.white;
    public Color strengthColor = Color.red;
    public Color objectColor = Color.green;

    //General
    List<int> detectIdList;
    List<long> croppedDistances;
    List<long> strengths;
    Vector3[] directions;
    bool directionCached = false;
    int drawCount;


    public enum DistanceCroppingMethod
    {
        RECT, RADIUS
    }


    void CalculateDistanceConstrainList(int steps)
    {
        switch (distanceCroppingMethod)
        {
            case DistanceCroppingMethod.RADIUS:
                for (int i = 0; i < steps; i++)
                {
                    distanceConstrainList[i] = maxDetectionDist;
                }
                break;

            case DistanceCroppingMethod.RECT:
                float keyAngle = Mathf.Atan(detectRectHeight / (detectRectWidth / 2f));

                for (int i = 0; i < steps; i++)
                {
                    if (directions[i].y <= 0)
                    {
                        distanceConstrainList[i] = 0;
                    }
                    else
                    {
                        float a = Vector3.Angle(directions[i], Vector3.right) * Mathf.Deg2Rad;
                        float tanAngle = Mathf.Tan(a);
                        float pn = tanAngle / Mathf.Abs(tanAngle);

                        float r = 0;
                        if (a < keyAngle || a > Mathf.PI - keyAngle)
                        {
                            float x = pn * detectRectWidth / 2;
                            float y = x * Mathf.Tan(a);
                            r = y / Mathf.Sin(a);
                        }
                        else if (a >= keyAngle && a <= Mathf.PI - keyAngle)
                        {
                            float angle2 = Mathf.PI / 2 - a;
                            float y = detectRectHeight;
                            float x = y * Mathf.Tan(angle2);
                            r = x / Mathf.Sin(angle2);
                        }

                        if (r < 0 || float.IsNaN(r))
                        {
                            r = 0;
                        }

                        distanceConstrainList[i] = (long)r;


                    }


                }

                break;
        }
    }

    List<long> ConstrainDetectionArea(List<long> beforeCrop, DistanceCroppingMethod method)
    {
        List<long> result = new List<long>();

        for (int i = 0; i < beforeCrop.Count; i++)
        {
            if (beforeCrop[i] > distanceConstrainList[i] || beforeCrop[i] <= 0)
            {
                result.Add(distanceConstrainList[i]);
            }
            else
            {
                result.Add(beforeCrop[i]);
            }
        }

        return result;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        //draw boundary
        Gizmos.DrawWireSphere(new Vector3(0, 0, 0) + transform.position, maxDetectionDist);
        Gizmos.DrawWireCube(new Vector3(0, detectRectHeight / 2, 0) + transform.position, new Vector3(detectAreaRect.width, detectAreaRect.height, 1));

        //draw distance rays
        if (debugDrawDistance && croppedDistances != null)
        {
            for (int i = 0; i < croppedDistances.Count; i++)
            {
                Vector3 dir = directions[i];
                long dist = croppedDistances[i];
                Debug.DrawLine(Vector3.zero + transform.position, (dist * dir) + transform.position, distanceColor);
            }
        }


        //draw objects detected
        if (detectedObjects == null) return;

        for (int i = 0; i < detectedObjects.Count; i++)
        {
            DetectObject obj = detectedObjects[i];

            Vector3 dir = directions[obj.medianId];
            long dist = obj.medianDist;


            if (drawObjectRays)
            {
                for (int j = 0; j < obj.distList.Count; j++)
                {
                    var myDir = directions[obj.idList[j]];
                    Debug.DrawLine(Vector3.zero + transform.position, (myDir * obj.distList[j]) + transform.position, objectColor);
                }
            }


            //center
            if (drawObjectCenterRay) Debug.DrawLine(Vector3.zero + transform.position, (dir * dist) + transform.position, Color.blue);


            //draw objects!
            if (drawObject) Gizmos.DrawWireCube((Vector3)obj.GetPosition(directions) + transform.position, new Vector3(100, 100, 0));

        }
    }
#endif


    private void OnGUI()
    {
        // https://sourceforge.net/p/urgnetwork/wiki/scip_jp/
        if (GUILayout.Button("VV: (Get Version Information)"))
        {
            urg.Write(SCIP_library.SCIP_Writer.VV());
        }
        //		if(GUILayout.Button("SCIP2")){
        //			urg.Write(SCIP_library.SCIP_Writer.SCIP2());
        //		}
        if (GUILayout.Button("PP: (Get Parameters)"))
        {
            urg.Write(SCIP_library.SCIP_Writer.PP());
        }
        if (GUILayout.Button("MD: (Measure and Transimission)"))
        {
            urg.Write(SCIP_library.SCIP_Writer.MD(0, 1080, 1, 0, 0));
        }
        if (GUILayout.Button("ME: (Measure Distance and Strength"))
        {
            urg.Write(SCIP_library.SCIP_Writer.ME(0, 1080, 1, 1, 0));
            open = true;
        }
        if (GUILayout.Button("BM: (Emit Laser)"))
        {
            urg.Write(SCIP_library.SCIP_Writer.BM());
        }
        if (GUILayout.Button("GD: (Measure Distance)"))
        {
            urg.Write(SCIP_library.SCIP_Writer.GD(0, 1080));
        }
        if (GUILayout.Button("GD_loop"))
        {
            gd_loop = !gd_loop;
        }
        if (GUILayout.Button("QUIT"))
        {
            urg.Write(SCIP_library.SCIP_Writer.QT());
        }

        //GUILayout.Label("distances.Count: " + distances.Count + " / strengths.Count: " + strengths.Count);
        //        GUILayout.Label("distances.Length: " + distances + " / strengths.Count: " + strengths.Count);
        // GUILayout.Label("drawCount: " + drawCount + " / detectObjects: " + detectedObjects.Count);
    }


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    // Use this for initialization
    private void Start()
    {
        croppedDistances = new List<long>();
        strengths = new List<long>();
        detectedDis = new List<float>();
        detectedDir = new List<Vector3>();
        urg = gameObject.AddComponent<UrgDeviceEthernet>();
        urg.StartTCP(ip_address, port_number);

        StartMeasureDistance();
    }

    private void StartMeasureDistance()
    {
        urg.Write(SCIP_library.SCIP_Writer.MD(0, 1080, 1, 0, 0));
    }

    private void CacheDirections()
    {
        float d = Mathf.PI * 2 / 1440;
        float offset = d * 540;
        directions = new Vector3[sensorScanSteps];
        for (int i = 0; i < directions.Length; i++)
        {
            float a = d * i + offset;
            directions[i] = new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0);
        }
    }


    private void Update()
    {

        if (smoothKernelSize % 2 == 0)
        {
            smoothKernelSize += 1;
        }

        List<long> originalDistances = new List<long>(urg.distances);

        if (originalDistances.Count <= 0)
        {
            return;
        }


        //Setting up things, one time
        if (sensorScanSteps <= 0)
        {
            sensorScanSteps = urg.distances.Count;
            distanceConstrainList = new long[sensorScanSteps];
            CacheDirections();

            CalculateDistanceConstrainList(sensorScanSteps);
        }

        if (gd_loop)
        {
            urg.Write(SCIP_library.SCIP_Writer.GD(0, 1080));
        }

        var cropped = ConstrainDetectionArea(originalDistances, distanceCroppingMethod);
        croppedDistances.Clear();
        croppedDistances.AddRange(cropped);


        if (smoothDistanceCurve)
        {
            croppedDistances = SmoothDistanceCurve(croppedDistances, smoothKernelSize);
        }
        if (smoothDistanceByTime)
        {
            croppedDistances = SmoothDistanceCurveByTime(croppedDistances, ref smoothByTimePreviousList, timeSmoothFactor);
        }


        //-----------------
        //  detect objects

        DetectObjects(croppedDistances, distanceConstrainList, ref detectedObjects);

    }

    private List<long> SmoothDistanceCurve(List<long> croppedDistances, int smoothKernelSize)
    {
        //TODO:
        return croppedDistances;
    }

    private void DetectObjects(List<long> croppedDistances, long[] distanceConstrainList, ref List<DetectObject> resultList, bool useCV = true)
    {

        if (resultList == null)
            resultList = new List<DetectObject>();
        else
            resultList.Clear();


        bool isGrouping = false;
        for (int i = 0; i < croppedDistances.Count; i++)
        {

            var dist = croppedDistances[i];
            var ubDist = distanceConstrainList[i];

            if (dist < ubDist - 20)
            {
                if (!isGrouping)
                {
                    isGrouping = true;
                    DetectObject newObject = new DetectObject();
                    newObject.startDist = i;
                    detectedObjects.Add(newObject);
                    isGrouping = true;

                }
                else
                {
                    var newObject = detectedObjects[detectedObjects.Count - 1];
                    newObject.idList.Add(i);
                    newObject.distList.Add(dist);
                }

            }
            else
            {
                if (isGrouping)
                {
                    isGrouping = false;
                }
            }
        }


        //trim off noise
        for (int i = 0; i < resultList.Count; i++)
        {
            if (resultList[i].idList.Count < noiseLimit)
            {
                resultList.RemoveAt(i);
            }
        }



    }



    List<long> SmoothDistanceCurveByTime(List<long> newList, ref List<long> previousList, float smoothFactor)
    {
        if (previousList.Count <= 0)
        {
            previousList = newList;
            return newList;
        }
        else
        {
            long[] result = new long[newList.Count];
            for (int i = 0; i < result.Length; i++)
            {

                float diff = newList[i] - previousList[i];
                float smallDiff = diff * smoothFactor;
                float final = previousList[i] + smallDiff;

                result[i] = (long)final;
                previousList[i] = result[i];
            }
            return result.ToList();
        }

    }


    [Serializable]
    private class DetectObject
    {
        public List<long> distList;
        public List<int> idList;

        public long startDist;

        public int medianId
        {
            get
            {
                int size = idList.Count;
                return idList[size / 2];
            }
        }

        public int averageId
        {
            get
            {
                return (int)(idList.Average());
            }
        }

        public double averageDist
        {
            get
            {
                return distList.Average();
            }
        }

        public long medianDist
        {
            get
            {
                int size = distList.Count;
                return distList[size / 2];
            }
        }



        public Vector2 GetPosition(in Vector3[] cachedDirs)
        {
            float angle = Vector3.Angle(cachedDirs[averageId], Vector3.right);
            float theta = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(theta) * (float)averageDist;
            float y = Mathf.Sin(theta) * (float)averageDist;
            return new Vector2(x, y);
        }


        public DetectObject()
        {
            distList = new List<long>();
            idList = new List<int>();
        }
    }
    // PP
    //	MODL ... 传感器信号类型
    //	DMIN ... 最小計測可能距離 (mm)
    //	DMAX ... 最大計測可能距離 (mm)
    //	ARES ... 角度分解能(360度の分割数)
    //	AMIN ... 最小可测量方向值
    //	AMAX ... 最大可测量方向值
    //	AFRT ... 正面方向値
    //	SCAN ... 標準操作角速度
}