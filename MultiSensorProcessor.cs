using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiSensorProcessor : MonoBehaviour
{
    [SerializeField]
    List<HKY.URGSensorObjectDetector> sensors;

    public int sensorDetectWidth { get; private set; }
    public int sensorDetectheight { get; private set; }
    public int sensorCount { get { return sensors.Count; } }

    void Awake()
    {
        //remove inactive sensor script
        sensors.RemoveAll(sensor => !sensor.gameObject.activeInHierarchy || !sensor.enabled);
        
        //calculate sensor's offset
        switch (sensors.Count)
        {
            case 0:
                Debug.LogWarning(this.name + ": No sensor found");
                break;
            case 1:
                Debug.LogWarning(this.name + "Found 1 sensor");
                sensorDetectWidth = sensors[0].detectRectWidth;
                sensorDetectheight = sensors[0].detectRectHeight;
                break;
            case 2:
                Debug.Log(this.name + "found 2 sensors");
                var leftSensor = sensors[0];
                var rightSensor = sensors[1];
                sensorDetectWidth = leftSensor.detectRectWidth + rightSensor.detectRectWidth;
                if (leftSensor.detectRectHeight == rightSensor.detectRectHeight) { sensorDetectheight = leftSensor.detectRectHeight; }
                else { Debug.LogError(this.name + "sensor 01 and sensor 02's heights are not equal!!!!"); }
                //========== Change Their Offset to Get The Combined Matrix ============
                //make it 0 -> detectRectWidth            
                leftSensor.positionOffset.x += leftSensor.detectRectWidth / 2;
                rightSensor.positionOffset.x += rightSensor.detectRectWidth / 2;
                //move right sensor to the right by the width of the left sensor
                rightSensor.positionOffset.x += leftSensor.detectRectWidth;

                //final
                leftSensor.positionOffset.x -= sensorDetectWidth / 2;
                rightSensor.positionOffset.x -= sensorDetectWidth / 2;
                //======================
                break;
            default:
                Debug.LogError(this.name + "too many sensors!");
                break;
        }
    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
