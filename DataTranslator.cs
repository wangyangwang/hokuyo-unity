using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HKY
{
    public class DataTranslator
    {
        public int xOffset;
        public int yOffset;
        public int sensorDetectWidth;
        public int sensorDetectHeight;

        public enum ZeroPosition
        {
            LEFT_TOP, LEFT_BOTTOM
        }

        public DataTranslator(int xOffset, int yOffset, int sensorDetectWidth, int sensorDetectHeight)
        {
            this.xOffset = xOffset;
            this.yOffset = yOffset;
            this.sensorDetectWidth = sensorDetectWidth;
            this.sensorDetectHeight = sensorDetectHeight;
        }

        /// <summary>
        /// translate point in sensor coordinate to screen coordinate.  (0,0) is at the Left-Bottom Corner
        /// </summary>
        /// <param name="inputData">input point in sensor coordinate</param>
        /// <returns></returns>
        public Vector2 Sensor2Screen(Vector2 inputData, ZeroPosition zeroPosition = ZeroPosition.LEFT_TOP)
        {
            //apply offset in mm
            inputData.x += xOffset;
            inputData.y += yOffset;

            //convert to 0 -> w
            inputData.x += (sensorDetectWidth / 2f);
            //flip
            inputData.x = sensorDetectWidth - inputData.x;

            //convert to 0 to 1
            inputData.x /= sensorDetectWidth;
            //convert to 0 to 1
            inputData.y /= sensorDetectHeight;

            if (zeroPosition == ZeroPosition.LEFT_BOTTOM)
            {
                //flip the Y
                inputData.y = 1 - inputData.y;
            }

            //now inputData's range is is x: 0->1  y: 0->1
            inputData.x *= Screen.width;
            inputData.y *= Screen.height;

            return inputData;
        }
    }
}