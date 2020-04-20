using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using Jh_Lib;

public struct StructUserData
{
    public string uid;
    public string posX;
    public string posY;

    public string opponentUid;

    public string bombIndex;
}

enum ErrorCode { Error = -1 }

public class ServerNet
{
    static public int BYTESIZE = 1024 * 3;

    List<Socket> mClinetSocketList;
    CrazyArcadeRequestHandler mRequestHandler;

    public ServerNet()
    {
        mRequestHandler = new CrazyArcadeRequestHandler();
        mRequestHandler.InitCrazyArcadeHandler();

        // 1. 서버 소켓 준비
        StartServerSocket();

    }

    void StartServerSocket()
    {
        IPEndPoint ip_point = new IPEndPoint(IPAddress.Any, 9999);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        socket.Bind(ip_point);
        mClinetSocketList = new List<Socket>();

        while (true)
        {
            socket.Listen(100);
            Socket client_socket = socket.Accept();

            // 소켓 연결되면 쓰레드에서 실행 시킴
            Thread client_thread = new Thread(() => RunClient(ref client_socket));
            mClinetSocketList.Add(client_socket);
            client_thread.Start();
        }
    }

    void RunClient(ref Socket socket)
    {
        Console.WriteLine("Socket Connect");

        String msg = "";
        while (true)
        {
            // 1. 통신을 받는다
            string recieve_msg = Receive(ref socket);
            if (recieve_msg == ErrorCode.Error.ToString())
                return;

            // 2. request에 알맞는 response를 만든다
            StructRequest response = MakeResponse(ref socket, recieve_msg);
            if (IsErrorStructRequest(response))
                return;

            // 3. response 전달
            bool is_success = Send(ref socket, response);
            if (!is_success)
                return;

        }
        socket.Close();
    }

    string Receive(ref Socket socket)
    {
        try
        {
            Byte[] data = new Byte[BYTESIZE];
            socket.Receive(data);
            String msg = Encoding.Default.GetString(data);
            Console.WriteLine("수신 : " + msg);
            return msg;
        }
        catch (Exception err)
        {
            Console.WriteLine("recieve error : Soket Close");
            SocektClose(socket);
            return ErrorCode.Error.ToString();
        }
    }

    bool Send(ref Socket socket, StructRequest response)
    {
        string send = NetFormatHelper.StructRequestToString(response);
        byte[] _data = new Byte[BYTESIZE];
        _data = NetFormatHelper.StringToByte(send);

        try
        {
            Console.WriteLine("송신 : " + send);
            socket.Send(_data);
            return true;

        }
        catch (Exception err)
        {
            Console.WriteLine("Send error : Soket Close");
            SocektClose(socket);
            return false;
        }
    }

    StructRequest MakeResponse(ref Socket socket, string rec_msg)
    {
        try
        {
            StructRequest request = NetFormatHelper.StringToStructRequest(rec_msg);
            StructRequest response = mRequestHandler.HandleRequest(request, ref socket);
            return response;
        }
        catch
        {
            SocektClose(socket);
            Console.WriteLine("null error msg :" + rec_msg);
            Console.WriteLine("null error : Soket Close");
            return ErrorStructRequest();
        }
    }

    void SocektClose(Socket socket)
    {
        socket.Close();
        mClinetSocketList.Remove(socket);

    }

    public static StructRequest ErrorStructRequest()
    {
        StructRequest req = new StructRequest();
        req.uid = ErrorCode.Error.ToString();
        return req;
    }

    bool IsErrorStructRequest(StructRequest req)
    {        
        return (req.uid == ErrorCode.Error.ToString());
    }

   
}


