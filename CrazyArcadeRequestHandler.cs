using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Jh_Lib;

class CrazyArcadeRequestHandler
{
    // 순차적으로 클라이언트에게 uid 부여
    int mUIDIndex = 0;

    Dictionary<string, StructUserData> mUserData;
    Socket mBombSocket1;
    Socket mBombSocket2;

    string mMapData; // 서버 로컬에 저장된 mapdata
    string mItemList;

    public void InitCrazyArcadeHandler()
    {
        // 0. 맵 정보 불러오기
        SetMapData();

        // 1. 아이템 리스트 작성
        SetItemList();
    }

    public StructRequest HandleRequest(StructRequest request, ref Socket socket)
    {
        string url = request.request_url;
        StructRequest response;

        if (url == URL.InitUser.ToString())
            return response = InitUserID(request);
        else if (url == URL.SyncMovement.ToString())
            return response = SyncMovement(request);
        else if (url == URL.AttackBomb.ToString())
            AttackBomb(request);

        else if (url == URL.GetOpponentData.ToString())
            return response = GetOpponentData(request);

        else if (url == URL.SetBombSocket.ToString())
            SetBombSocket(request, ref socket);

        return new StructRequest();
    }


    /* @ url : InitUser
     * @ request :
     * @ response :
     *      uid
     *      mapData
     */
    StructRequest InitUserID(StructRequest request)
    {
        int user_id = mUIDIndex % 2; // uid는 0 과 1 만 발급
        mUIDIndex += 1;

        if (mUserData == null)
            mUserData = new Dictionary<string, StructUserData>();

        // 유저 데이터 생성
        StructUserData user_data = new StructUserData();
        user_data.uid = user_id.ToString();

        // 파라매터 생성
        Dictionary<string, string> dic_param = new Dictionary<string, string>();
        dic_param["mapData"] = mMapData;
        dic_param["itemData"] = mItemList;

        // 유저 데이터에 저장
        request.uid = user_data.uid;
        mUserData[user_id.ToString()] = user_data;


        return MakeResponse(request, dic_param);
    }

    /* @ url : SyncMovement
    * @ request :
    *       posX
    *       posY
    *       opponentUid
    * @ response :
    *      opponentPosX
    *      opponentPosY
    */
    StructRequest SyncMovement(StructRequest request)
    {

        string user_id = request.uid;

        // 유저가 없을 때
        if (!mUserData.ContainsKey(user_id))
            return ServerNet.ErrorStructRequest();

        StructUserData user_data = mUserData[user_id];

        // 파라매터가 오지 않았을 경우
        if (request.parameter == null)
            return ServerNet.ErrorStructRequest();

        // 유저 위치 기록
        if (request.parameter.ContainsKey("posX"))
            user_data.posX = request.parameter["posX"];

        if (request.parameter.ContainsKey("posY"))
            user_data.posY = request.parameter["posY"];

        if (request.parameter.ContainsKey("opponentUid"))
            user_data.opponentUid = request.parameter["opponentUid"];


        mUserData[user_id] = user_data;

        // 상대방 정보
        if (!request.parameter.ContainsKey("opponentUid"))
            return ServerNet.ErrorStructRequest();

        string opponentUid = request.parameter["opponentUid"];

        // 상대방 정보 없을 경우
        if (!mUserData.ContainsKey(opponentUid))
            return ServerNet.ErrorStructRequest();

        StructUserData opponent_data = mUserData[opponentUid];

        // 상대방 위치를 response로 전달
        Dictionary<string, string> dic_param = new Dictionary<string, string>();
        dic_param["opponentPosX"] = opponent_data.posX == null ? "0" : opponent_data.posX;
        dic_param["opponentPosY"] = opponent_data.posY == null ? "0" : opponent_data.posY;
        return MakeResponse(request, dic_param);

    }



    /* @ url : AttackBomb
    * @ request :
    * @ response :
    *      bombIndex
    */

    // @Send 함수가 별도로 있음 주의할 것
    void AttackBomb(StructRequest request)
    {
        string user_id = request.uid;

        // 유저가 없을 경우
        if (!mUserData.ContainsKey(user_id))
            return;

        // 물풍선 위치 저장
        StructUserData user_data = mUserData[user_id];
        if (request.parameter.ContainsKey("bombIndex"))
            user_data.bombIndex = request.parameter["bombIndex"];

        // 바로 쏴주기 떄문에 사실 필요는 없다.
        mUserData[user_id] = user_data;

        // 파라매터
        Dictionary<string, string> dic_param = new Dictionary<string, string>();
        dic_param["bombIndex"] = user_data.bombIndex;
        StructRequest res = MakeResponse(request, dic_param);

        // 상대 클라이언트 소켓에 물풍선 통신
        Socket socket;
        if (request.uid == "0")
            socket = mBombSocket1;
        else
            socket = mBombSocket2;

        string send = NetFormatHelper.StructRequestToString(res);
        byte[] _data = new Byte[ServerNet.BYTESIZE];
        _data = NetFormatHelper.StringToByte(send);

        try
        {
            Console.WriteLine("수신 : " + send);
            socket.Send(_data);

        }
        catch (Exception err)
        {
            Console.WriteLine("Send error");
            return;
        }
    }



    void SetBombSocket(StructRequest request, ref Socket socket)
    {
        if (request.uid == "0")
            mBombSocket2 = socket;
        else
            mBombSocket1 = socket;
    }

    /* @ url : GetOpponentData
   * @ request :
   * @ response :
   *      opponentUid
   */
    StructRequest GetOpponentData(StructRequest request)
    {
        string user_uid = request.uid;
        string opponent_uid = ErrorCode.Error.ToString();
       
        if (user_uid == "1")
            opponent_uid = "0";
        else
            opponent_uid = "1";
      
        if (mUserData.ContainsKey(opponent_uid))
        {
            StructUserData opponent_data = mUserData[opponent_uid];
            Dictionary<string, string> dic_param = new Dictionary<string, string>();
            dic_param["opponentUid"] = opponent_data.uid;
            return MakeResponse(request, dic_param);
        }

        return ServerNet.ErrorStructRequest();
    }


    StructRequest MakeResponse(StructRequest request, Dictionary<string, string> dic)
    {
        StructRequest response = new StructRequest();
        response.uid = request.uid;
        response.request_url = request.request_url;
        response.parameter = dic;

        return response;
    }

    void SetMapData()
    {
        mMapData = LoadData();
    }

    string LoadData()
    {
        string path = System.IO.Directory.GetCurrentDirectory() + "../../../../MapData.json";
        System.IO.FileInfo file = new System.IO.FileInfo(path);
        if (file.Exists)
        {
            return File.ReadAllText(path);
        }
        return "";
    }

    void SetItemList()
    {
        Random rand = new Random();
        // 0 - 4 아이템
        // 5 no 아이템
        for (int i = 0; i < 100; i++)
        {
            int res = rand.Next(0, 5);
            mItemList += res.ToString();
        }

    }

}

