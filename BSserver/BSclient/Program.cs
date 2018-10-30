using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;


namespace BSClient
{
    public class BSclientData
    {
        /* --클라이언트 데이터 클래스 생성-- */
        public string macAddress;
        public string exitConnection;
        public string nickName;
        public string ufoLife;
        public string ufoPosition;
        public string ufoVelocity;
        public string ufoRotation;
        public string hittedUfoMac;
        public List<Bullet> bullet = new List<Bullet>();
        public class Bullet
        {
            public string position;
            public string velocity;
            public string remainingTime;
        }
    }

    public class BSClientProgram
    {
        /* --맥 주소 저장용-- */
        public static string macAddress = NetworkInterface.GetAllNetworkInterfaces()[0].GetPhysicalAddress().ToString(); // 보낼 데이터 저장용 문자열 정의
                                                                                                                         
        /* --전역 메인 스레드 동기화 신호 생성-- */
        public static ManualResetEvent clientAllDone = new ManualResetEvent(false);

        /* --전역 송수신 스레드 동기화 신호 생성-- */
        public static ManualResetEvent sendAllDone = new ManualResetEvent(false);

        /* --전역 소켓 생성-- */
        public static Socket workingSocket = null; // 핸들러 저장용(클라이언트 송수신용) 전역 소켓 정의

        /* --전역 데이터 저장용 문자열 변수 생성-- */
        public static string sendData = ""; // 보낼 데이터 저장용 문자열 정의
        public static string receiveData = ""; // 받은 데이터 저장용 문자열 정의

        /* --전역 데이터 저장용 버퍼 생성-- */
        public static byte[] sendBuffer = new byte[1024]; // 보낼 데이터 저장용 버퍼 정의
        public static byte[] receiveBuffer = new byte[1024]; // 받은 데이터 저장용 버퍼 정의

        /* --개별 클라이언트 데이터 저장을 위한 클래스 생성-- */
        public static BSclientData clientData = new BSclientData();
        

        /* --소켓 연결 함수-- */
        static void ConnectCall(IAsyncResult iar)
        {
            // 강제로 Server 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                Console.WriteLine("연결을 승인받는 중...\n");

                // 토커(서버) 소켓 정의
                Socket talkerSocket = (Socket)iar.AsyncState;

                // 토커(서버) 소켓을 추후 송수신을 위해 전역 변수로 임시 저장   
                workingSocket = talkerSocket;

                // 연결을 승인 받았음
                workingSocket.EndConnect(iar);

                // 서버 연결정보 표시
                string serverAddress = workingSocket.RemoteEndPoint.ToString(); // 서버 IP, Port 가져오기 (예시 123.123.123.123:1234)
                Console.WriteLine("서버 연결됨 -> " + serverAddress + "\n");

                while (true)
                {
                    // 서버와 이미 송수신 중이기 때문에 송수신 함수 스레드 일시중지
                    sendAllDone.Reset();

                    // 송수신 전에 송신할 데이터 조합
                    CollectData();

                    // 데이터 송수신 시작
                    workingSocket.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None,
                    new AsyncCallback(SendCall), workingSocket);

                    // 여유 대기시간
                    Thread.Sleep(200);

                    // 만약 서버 종료 신호를 보냈을 경우 연결 종료
                    if (clientData.exitConnection == "T")
                    {
                        // 송수신 완료 후 연결 종료
                        workingSocket.Shutdown(SocketShutdown.Both);
                        workingSocket.Close();

                        // 여유 대기시간
                        Thread.Sleep(200);

                        // 연결 종료되면 로그인 화면 다시 출력 (유니티에서 처리해야하는 부분)

                        // 프로그램 종료
                        Environment.Exit(0);
                    }

                    // 서버와 연결이 이미 대기 중인지 확인하고 송수신 함수 스레드 계속 진행
                    sendAllDone.WaitOne();
                }
            }
            catch (Exception exc) // 예외사항 발생시 연결 종료
            {
                Console.WriteLine("클라이언트와 연결 실패...\n" + exc);
            }
        }


        /* --송신 전 데이터 조합 함수 (가짜 클라이언트 테스트용)-- */
        static void CollectData()
        {
            // 클라이언트에서 맥 어드레스 받기
            Console.WriteLine("맥 어드레스를 받아왔습니다...\n");
            Console.WriteLine(macAddress + "\n");

            // 클라이언트에서 연결 종료 여부 받기 ("T" == 종료처리 , "F" == 종료안함)
            Console.WriteLine("다음 분기에서 서버와 연결을 종료하려면 T 아니면 F 를 입력하세요...\n");
            clientData.exitConnection = Console.ReadLine();

            // 클라이언트에서 닉네임 받기 (한글, 영문, 숫자 최대 10자)
            Console.WriteLine("접속을 위해 유저 이름을 입력하세요...\n");
            clientData.nickName = Console.ReadLine();

            // 클라이언트에서 남은 UFO 목숨값 받기 (3,2,1)
            Console.WriteLine("UFO의 남은 목숨을 입력하세요...\n");
            clientData.ufoLife = Console.ReadLine();

            // 클라이언트에서 UFO 위치값 받기 (x,y)
            Console.WriteLine("UFO의 위치를 입력하세요...\n");
            clientData.ufoPosition = Console.ReadLine();

            // 클라이언트에서 UFO 벡터값 받기
            Console.WriteLine("UFO의 벡터를 입력하세요...\n");
            clientData.ufoVelocity = Console.ReadLine();

            // 클라이언트에서 UFO 회전값 받기
            Console.WriteLine("UFO의 회전값을 입력하세요...\n");
            clientData.ufoRotation = Console.ReadLine();

            // 클라이언트에서 내가 때린 UFO 맥 주소 받기
            Console.WriteLine("내가 때린 UFO 맥 주소를 입력하세요...\n");
            clientData.hittedUfoMac = Console.ReadLine();

            // 모든 문자열 합치기
            sendData = macAddress + "|" + clientData.exitConnection + "|" + clientData.nickName +
            "|" + clientData.ufoPosition + "|" + clientData.ufoVelocity + "|" + clientData.ufoRotation +
            "|" + clientData.hittedUfoMac + "|" + "Bullets...";
            
            // 보낼 데이터 바이트로 변환 후 송신 버퍼에 저장
            sendBuffer = Encoding.UTF8.GetBytes(sendData);
        }


        /* --소켓 데이터 송신 함수-- */
        static void SendCall(IAsyncResult iar)
        {
            // 강제로 Server 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                Console.WriteLine("데이터를 송신하는 중...\n");
                
                // 데이터 송신
                int sendSize = workingSocket.EndSend(iar);
                Console.WriteLine("보낸 데이터 크기 -> " + sendSize + "\n");

                // 송신이 끝났으니 이제 데이터 수신
                workingSocket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                new AsyncCallback(ReceiveCall), workingSocket);
            }
            catch (Exception exc)
            {
                Console.WriteLine("데이터 송신 실패...\n" + exc + "\n");
            }
        }


        /* --소켓 데이터 수신 함수-- */
        static void ReceiveCall(IAsyncResult iar)
        {
            //강제로 Server 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                Console.WriteLine("데이터를 수신받는 중...\n");

                // 핸들러(클라이언트) 소켓을 통해 데이터 수신받고 사이즈 저장
                int receiveSize = workingSocket.EndReceive(iar);
                Console.WriteLine("받은 데이터 크기 -> " + receiveSize);

                // 받을 데이터가 있는지 확인 (데이터 길이가 0보다 크면)
                if (receiveSize > 0)
                {
                    // 만약 마지막으로 받은 버퍼에서 EOF(문장의 끝)을 발견하지 못한 경우 데이터 계속 받음
                    if (receiveBuffer[receiveBuffer.Length - 1] != '\0')
                    {
                        // 데이터가 아직 다 수신되지 않았기 때문에 계속 호출하여 받아야함
                        workingSocket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                        new AsyncCallback(ReceiveCall), workingSocket);
                    }
                    else // 데이터가 다 수신되었음
                    {
                        // 버퍼의 내용을 수신 데이터 변수에 저장
                        receiveData = Encoding.UTF8.GetString(receiveBuffer, 0, receiveSize);
                        Console.WriteLine("받은 데이터 -> " + receiveData + "\n");

                        // 메세지 | 단위로 잘라서 저장
                        string[] receiveSplitData = receiveData.Split('|');
                        Console.WriteLine("자른 데이터는 ->\n");

                        // 자른 메세지 표시
                        for (int i = 0; i < receiveSplitData.Length; ++i)
                        {
                            Console.WriteLine(receiveSplitData[i]);
                        }
                    }
                }
                else
                {
                    // 수신받을 데이터가 없으면 수신 종료
                    Console.WriteLine("수신받을 데이터 없음...\n");
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("데이터 수신 실패...\n" + exc + "\n");
            }

            // 송수신 함수 스레드 계속 진행하도록 허락해 계속 송수신 처리
            sendAllDone.Set();
        }


        /* --메인 함수-- */
        public static void Main(string[] args)
        {
            //강제로 Server 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                IPAddress clientIp = IPAddress.Parse("127.0.0.1"); // 클라이언트 IP 정의
                IPEndPoint clientEndPoint = new IPEndPoint(clientIp, 8282); // 클라이언트 포트번호 저장
                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // 클라이언트 소켓 정의

                // 서버와 연결이 이미 대기 중이기 때문에 메인 함수 스레드 일시중지
                clientAllDone.Reset();

                // 서버와 연결 대기 스레드 할당
                Console.WriteLine("서버의 연결을 기다림...\n");
                clientSocket.BeginConnect(clientEndPoint, new AsyncCallback(ConnectCall), clientSocket);

                // 서버와 연결이 이미 대기 중인지 확인하고 메인 함수 스레드 계속 진행
                clientAllDone.WaitOne();
            }
            catch (Exception exc)
            {
                Console.WriteLine("서버와 연결 실패...\n" + exc + "\n");
            }
        }
    }
}
