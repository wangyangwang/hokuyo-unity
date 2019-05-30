using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace HKY
{
    public class RawObject
    {
        public int medianId { get { return idList[idList.Count / 2]; } }
        public int averageId { get { return (int)(idList.Average()); } }
        public double averageDist { get { return distList.Average(); } }
        public long medianDist { get { return distList[distList.Count / 2]; } }
        public float size
        {
            get
            {
                Vector2 pointA = CalculatePosition(cachedDirs[idList[0]], distList[0]);
                Vector2 pointB = CalculatePosition(cachedDirs[idList[idList.Count - 1]], distList[distList.Count - 1]);
                return Vector2.Distance(pointA, pointB);
            }
        }

        public List<long> distList;
        public List<int> idList;

        private readonly Vector3[] cachedDirs;

        Vector2 _position = Vector2.zero;
        //position will be set once to save computing power
        bool positionSet = false;
        public Vector2 position
        {
            get
            {
                if (!positionSet) Debug.LogError("position has not bee set yet");
                return _position;
            }
            set { _position = value; }
        }

        public void GetPosition()
        {
            position = CalculatePosition();
            positionSet = true;
        }

        Vector2 CalculatePosition()
        {
            return CalculatePosition(cachedDirs[medianId], medianDist);
        }

        Vector2 CalculatePosition(Vector3 dir, float dist)
        {
            float angle = Vector3.Angle(dir, Vector3.right);
            float theta = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(theta) * dist;
            float y = Mathf.Sin(theta) * dist;
            return new Vector2(x, y);
        }

        public RawObject(in Vector3[] cachedDirs, int id)
        {
            distList = new List<long>();
            idList = new List<int>();
            //save cached direction data
            this.cachedDirs = cachedDirs;
        }
    }

    [System.Serializable]
    public class ProcessedObject
    {
        static readonly int MISSING_FRAME_LIMIT = 5;
        public readonly System.Guid guid;
        public Vector3 position { get; private set; }
        public Vector3 deltaMovement { get; private set; }
        public float age { get { return Time.time - birthTime; } }

        public float size;
        public float birthTime;
        public int missingFrame = 0;
        public bool clear { get; private set; }
        public bool useSmooth = true;

        Vector3 currentVelocity;
        Vector3 oldPosition;
        float posSmoothTime = 0.2f;

        public ProcessedObject(Vector3 position, float size, float objectPositionSmoothTime = 0.2f)
        {
            guid = System.Guid.NewGuid();
            this.position = position;
            this.size = size;
            posSmoothTime = objectPositionSmoothTime;

            currentVelocity = new Vector3();
            birthTime = Time.time;
        }

        public static ProcessedObject Clone(ProcessedObject obj)
        {
            Debug.Log(obj.guid.ToString() + " test before " + obj.position);
            //  ProcessedObject newObj = new ProcessedObject(obj.position, obj.size, obj.posSmoothTime);
            var s = JsonUtility.ToJson(obj);
            var newObj = JsonUtility.FromJson<ProcessedObject>(s);
            Debug.Log(obj.guid.ToString() + "test after" + newObj.position);
            return newObj;
        }

        //update with a new position
        public void Update(Vector3 newPos, float newSize)
        {
            size = newSize;
            oldPosition = position;

            if (useSmooth)
            {
                position = Vector3.SmoothDamp(position, newPos, ref currentVelocity, posSmoothTime);
            }
            else
            {
                position = newPos;
            }
            missingFrame = 0;

            deltaMovement = position - oldPosition;

        }
        //update without a new position
        public void Update()
        {
            missingFrame++;
            if (missingFrame >= MISSING_FRAME_LIMIT)
            {
                clear = true;
            }
        }
    }

}
