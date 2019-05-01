using UnityEngine;
using System.Collections;

public class UrgDevice : MonoBehaviour {

	public enum CMD
	{
		// https://www.hokuyo-aut.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf
		VV, PP, II, // 传感器信息请求命令（3种） 
        BM, QT, //测量开始/结束命令
        MD, GD, // 距离请求命令（2种） 
        ME //距离和接收光强度请求命令 
    }

	public static string GetCMDString(CMD cmd)
	{
		return cmd.ToString();
	}
}
