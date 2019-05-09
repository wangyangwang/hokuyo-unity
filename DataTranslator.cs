using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataTranslator : MonoBehaviour
{
    //todo: make non-rect work
    public static Vector2 TranslateSensorDataToScreenPos(Vector2 inputData, HKY.URGSensorObjectDetector objectDetector, int widthOffset = 0, int heightOffset = 0)
    {
        //convert to 0 -> w
        inputData.x += (objectDetector.detectRectWidth / 2f);
        //flip
        inputData.x = objectDetector.detectRectWidth - inputData.x;
        //convert to 0 to 1
        inputData.x /= objectDetector.detectRectWidth;

      //  inputData.y = objectDetector.detectRectHeight - inputData.y;
        //convert to 0 to 1
        inputData.y /= objectDetector.detectRectHeight;

        //now inputData's range is is x: 0->1  y: 0->1
        inputData.x *= Screen.width;
        inputData.y *= Screen.height;

        //offset
        inputData.x += widthOffset;
        inputData.y += heightOffset;

        return inputData;
    }
}
