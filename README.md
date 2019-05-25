# hokuyo-unity

...WORKING IN PROGRESS...

this is modified based on https://github.com/inoook/UnityURG

*code is still at a very primitive state, object detection can be improved*


improvments and contributions are very welcomed.


this library works with multiple Hokuyo sensors, you need to give each of them a unique IP.

why
=====

I need to make a large wall into a "touch screen" so all code here are wrote to server this single purpose.

for example, the _strength_ data from the sensor is compeltely ignored here since it doesn't help us detecting objects on a surface. (as the sensor can detect things from pretty far!)

to better "converting a surface into a touch screen", I added the _distance cropping mechnaism_, to help ignore the things that are out of the detection area.


there are two ways to constrain the detection area:

- Rect
![rect](https://github.com/wangyangwang/hokuyo-unity/blob/master/rect.PNG)
- Radius
![rad](https://github.com/wangyangwang/hokuyo-unity/blob/master/rad.png)

if you choose `Rect` Mode, the sensor will only detect things within a rect area. You can change the _width_ and _height_ of the rect area by changing `detectRectWidth` and `detectRectHeight` the unit is *mm* (same as everything else!)

if you choose `Radius` Mode, the detection area will be a circular sector, the detection distance will be the same for all direction, you can change the detection radius by changing `maxDetectionDist`




Either Distance Cropping Mode will ignore things behind the sensor. which means 90 radians of detectable area will be wasted.

Prepare
====
you will need to do some setting up in the network setting of Windows befor you can get any data:

https://sourceforge.net/p/urgnetwork/wiki/ip_address_en/


Files:
========


- `URGSensorObjectDetector`

this is the class for "detecting" object from sensor raw data

- `UrgDeviceEthernet`

This is where we get the raw data from the sensor

- `SCIP_library`

Used by UrgDeviceEthernet, for low-level communication with the sensor


How to use
======

Attach _URGSensorObjectDetector_ to an empty GameObject or use the Prefab _Sensor Data_




Warning
===

*I only have two 	**Hokuyo UST-10LX**  so obviously this code is not tested on any other Hokuyo sensor model... but it should work with most of them!*
