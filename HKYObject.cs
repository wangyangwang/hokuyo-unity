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

        public List<long> distList;
        public List<int> idList;

        private readonly Vector3[] cachedDirs;

        public Vector2 CalculatePosition()
        {
            float angle = Vector3.Angle(cachedDirs[medianId], Vector3.right);
            float theta = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(theta) * (float)medianDist;
            float y = Mathf.Sin(theta) * (float)medianDist;
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


    public class ProcessedObject
    {

        static readonly int MISSING_FRAME_LIMIT = 10;

        public readonly System.Guid guid;
        public Vector3 position { get; private set; }
        public float age
        {
            get
            {
                return Time.time - birthTime;
            }
        }
        public float width;
        public float birthTime;
        public int missingFrame = 0;
        public bool clear { get; private set; }

        public bool useSmooth = true;

        Vector3 currentVelocity;
        public float smoothTime = 0.3f;

        public ProcessedObject(Vector3 position, float width = 100)
        {
            guid = System.Guid.NewGuid();
            this.position = position;
            this.width = width;

            currentVelocity = new Vector3();
            birthTime = Time.time;
        }

        //update with a new position
        public void Update(Vector3 newPos)
        {
            if (useSmooth)
            {
                position = Vector3.SmoothDamp(position, newPos, ref currentVelocity, smoothTime);
            }
            else
            {
                position = newPos;
            }

            missingFrame = 0;
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
