using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HKY
{
    // http://sourceforge.net/p/urgnetwork/wiki/top_jp/
    // https://www.hokuyo-aut.co.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf
    public class URGSensorObjectDetector : MonoBehaviour
    {

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
        [Range(1, 40)] public int noiseLimit = 7;
        List<Vector3> detectedDir;
        List<float> detectedDis;
        List<RawObject> rawObjectList;

        [Header("Object Tracking")]
        /// <summary>
        /// after object tracking, ProcessedObject exist across frames
        /// </summary>
        public List<ProcessedObject> detectedObjects;
        public float distanceThresholdForMerge = 300;

        public static System.Action<Guid> OnNewObject;
        public static System.Action<Guid> OnLoseObject;

        [Header("Debug Draw")]
        public bool debugDrawDistance = false;
        public bool drawObjectRays;
        public bool drawObjectCenterRay;
        public bool drawObject;
        public bool drawProcessedObject;

        //colors
        public Color distanceColor = Color.white;
        public Color strengthColor = Color.red;
        public Color objectColor = Color.green;
        public Color processedObjectColor = Color.cyan;

        //General
        List<long> croppedDistances;
        List<long> strengths;
        Vector3[] directions;

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
            //draw raw objects
            if (rawObjectList == null) return;
            for (int i = 0; i < rawObjectList.Count; i++)
            {
                var obj = rawObjectList[i];
                if (obj.idList.Count == 0 || obj.distList.Count == 0) return;
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
                if (drawObject) Gizmos.DrawWireCube((Vector3)obj.CalculatePosition() + transform.position, new Vector3(100, 100, 0));
            }

            if (drawProcessedObject)
            {
                //draw processed object
                foreach (var pObj in detectedObjects)
                {
                    Gizmos.color = processedObjectColor;
                    Gizmos.DrawCube(pObj.position, new Vector3(pObj.width, pObj.width, 1));
                }
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

        // Use this for initialization
        private void Start()
        {
            croppedDistances = new List<long>();
            strengths = new List<long>();
            detectedDis = new List<float>();
            detectedDir = new List<Vector3>();
            detectedObjects = new List<ProcessedObject>();

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


            List<long> originalDistances = new List<long>();
            lock (urg.distances)
            {
                if (urg.distances.Count <= 0) return;
                originalDistances = new List<long>(urg.distances);
            }
            if (originalDistances.Count <= 0) return;


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


            UpdateObjectList();
        }

        private List<long> SmoothDistanceCurve(List<long> croppedDistances, int smoothKernelSize)
        {
            //TODO:
            return croppedDistances;
        }

        private List<RawObject> DetectObjects(List<long> croppedDistances, long[] distanceConstrainList)
        {
            if (directions.Length <= 0)
            {
                Debug.LogError("directions array is not setup.");
                return new List<RawObject>();
            }

            int objectId = 0;

            var resultList = new List<RawObject>();
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
                        RawObject newObject = new RawObject(directions, objectId++);
                        resultList.Add(newObject);

                        isGrouping = true;
                    }
                    else
                    {
                        var newObject = resultList[resultList.Count - 1];
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

            resultList.RemoveAll(item => item.idList.Count < noiseLimit);
            return resultList;
        }


        void UpdateObjectList()
        {
            List<HKY.RawObject> newObjects = DetectObjects(croppedDistances, distanceConstrainList);
            rawObjectList = new List<RawObject>(newObjects);

            //update existing objects
            if (detectedObjects.Count != 0)
            {
                foreach (var oldObj in detectedObjects)
                {
                    Dictionary<RawObject, float> objectByDistance = new Dictionary<RawObject, float>();
                    //calculate all distance between existing objects and newly found objects
                    foreach (var newObj in newObjects)
                    {
                        float distance = Vector3.Distance(newObj.CalculatePosition(), oldObj.position);
                        objectByDistance.Add(newObj, distance);
                    }

                    if (objectByDistance.Count <= 0)
                    {
                        oldObj.Update();
                    }
                    else
                    {
                        //find the closest new obj and check if the dist is smaller than distanceThresholdForMerge, if yes, then update oldObj's position to this newObj
                        var closest = objectByDistance.Aggregate((l, r) => l.Value < r.Value ? l : r);
                        if (closest.Value <= distanceThresholdForMerge)
                        {
                            oldObj.Update(closest.Key.CalculatePosition());
                            //remove the newObj that is being used
                            newObjects.Remove(closest.Key);
                        }
                        else
                        {
                            //this oldObj cannot find a new one that is close enough to it
                            oldObj.Update();
                        }
                    }

                }

                //remove all missed objects
                for (int i = 0; i < detectedObjects.Count; i++)
                {
                    var obj = detectedObjects[i];
                    if (obj.clear)
                    {
                        if (OnLoseObject != null) { OnLoseObject(obj.guid); }
                        detectedObjects.RemoveAt(i);
                    }
                }

                //create new object for those newobject that cannot find match from the old objects
                foreach (var leftOverNewObject in newObjects)
                {
                    var newbie = new ProcessedObject(leftOverNewObject.CalculatePosition());
                    detectedObjects.Add(newbie);
                    if (OnNewObject != null)
                    {
                        OnNewObject(newbie.guid);
                    }
                }
            }
            else //add all raw objects into detectedObjects
            {
                foreach (var obj in rawObjectList)
                {
                    var newbie = new ProcessedObject(obj.CalculatePosition());
                    detectedObjects.Add(newbie);
                    // if (OnNewObject != null)
                    // {
                    //     OnNewObject(newbie.guid);
                    // }
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
}