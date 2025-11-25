#pragma warning disable 0168
#pragma warning disable 0219
#pragma warning disable 0414
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public enum LocationState {
    Disabled,
    TimedOut,
    Failed,
    Enabled
}

public class GPSReceiver : MonoBehaviour {
    coordData _preLocation = null;

    public bool 
        isFirstLoc,
        firstLocSend;

    float time = 0;
    private const float gpsInterval = 1f;
    private LocationState state;
    LocationInfo currGPSInfo;
    ArrayList firstLocArr = new ArrayList();

    void Update() {
        if(!isFirstLoc) {
            time += Time.deltaTime;
        }
    }

    IEnumerator Start() {
        isFirstLoc = true;
        firstLocSend = false;
        //GPS 허용이 켜져있지 않으면 종료한다.
        state = LocationState.Disabled;
        //StartCoroutine("getData");
        if (!Input.location.isEnabledByUser) {
            yield break;
        }

        //locationService를 시작한다.
        Input.location.Start(1, 1);

        int maxWait = 20;
        //locationService가 켜지고 있는 상태이거나 최대 대기시간이 아직 되지 않은 경우 대기한다.
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0) {
            yield return new WaitForSeconds(1);
            maxWait--;
        }
        //최대 대기 시간을 초과한 경우 종료한다.
        if (maxWait < 1) {
            state = LocationState.TimedOut;
            Debug.Log("Timed Out");
            yield break;
        }

        //locationService 연결이 실패한 경우 종료한다.
        if (Input.location.status == LocationServiceStatus.Failed) {
            state = LocationState.Failed;
            yield break;
        }

        //location 접근
        else {
            state = LocationState.Enabled;
            StartCoroutine("getData");
        }
    }

    IEnumerator getData() {
        //Action 생성
        GetGPSDataAction action = (GetGPSDataAction)ActionCreator.createAction(ActionTypes.GET_GPS_DATA);
        GameManager gameManager = GameManager.Instance;
        bool result;
        while (true) {
            //Debug.Log("GET_GPS_DATA Action 발생시킴");
            if(isFirstLoc) {
                Debug.Log("First Loc");
                currGPSInfo = Input.location.lastData;
                coordData data = new coordData(currGPSInfo.latitude, currGPSInfo.longitude, currGPSInfo.altitude, currGPSInfo.timestamp, currGPSInfo.horizontalAccuracy, currGPSInfo.verticalAccuracy);
                if (_filter(data)) {
                    firstLocArr.Add(data);
                } // 필터 적용

                //첫 data
                if (_preLocation == null) {
                    _preLocation = data;
                }
                
            }
            else {
                coordData data;
                if (!firstLocSend) {
                    //Debug.Log("좌표 평균 구한 후 초기 좌표 보내기");
                    data = setFirstLoc();
                    firstLocSend = true;
                }
                else {
                    //Debug.Log("일반 좌표 보내기");
                    currGPSInfo = Input.location.lastData;
                    coordData currCoord = new coordData(currGPSInfo.latitude, currGPSInfo.longitude, currGPSInfo.altitude, currGPSInfo.timestamp, currGPSInfo.horizontalAccuracy, currGPSInfo.verticalAccuracy);
                    data = currCoord;
                }
                
                TimeSpan timeSpane = TimeSpan.FromSeconds(time);
                string timeText = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpane.Hours, timeSpane.Minutes, timeSpane.Seconds);
                //Debug.Log(timeText);
                action.GPSInfo = data;
                action.timeText = timeText;
                gameManager.gameDispatcher.dispatch(action);
            }
            //Debug.Log(timeText);

            yield return new WaitForSeconds(gpsInterval);
        }
    }

    public bool _filter(coordData loc) {
        if (loc.timeStamp == 0) { return false; }
        if (_preLocation == null) { return true; }
        if (loc.timeStamp <= _preLocation.timeStamp) { return false; }

        return true;
    }

    coordData setFirstLoc() {
        float sumLat = 0;
        float sumLon = 0;
        float altitude = 0;
        double timeStamp = 0;
        float horizontalAcuracy = 0;
        float verticalAcuracy = 0;

        foreach (coordData data in firstLocArr) {
            sumLat += data.latitude;
            sumLon += data.longitude;
            altitude = data.altitude;
            timeStamp = data.timeStamp;
            horizontalAcuracy = data.horizontalAcuracy;
            verticalAcuracy = data.verticalAcuracy;
        }
        //Debug.Log(sumLat);
        //Debug.Log(sumLon);
        //Debug.Log(sumLon / firstLocArr.Count);
        //Debug.Log(sumLat / firstLocArr.Count);
        coordData result = new coordData(sumLon / firstLocArr.Count, sumLat / firstLocArr.Count, altitude, timeStamp, horizontalAcuracy, verticalAcuracy);

        firstLocArr.Clear();
        return result;
    }
}
