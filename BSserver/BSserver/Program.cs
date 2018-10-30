using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace BSserver
{
    public class BSclientData
    {
        /* --클라이언트 데이터 클래스 생성-- */
        public string macAddress;
        public string exitConnection;
        public string nickName;
        public string ufoPosition;
        public string ufoVelocity;
        public string ufoRotation;
        public string[] hittedUfoMac = new string[10];
        public List<Bullet> bullet = new List<Bullet>();
        public class Bullet
        {
            public string position;
            public string velocity;
            public string remainingTime;
        }
    }

    public class BSstateBuffer
    {
        /* --개별 핸들러(클라이언트)의 갯수 카운터 생성-- */
        public int clientCounter = 0;

        /* --개별 수신 데이터 저장용 문자열 변수 생성-- */
        public string receiveData = ""; // 받은 데이터 저장용 문자열 정의

        /* --개별 수신 데이터 저장용 버퍼 생성-- */
        public byte[] receiveBuffer = new byte[1024]; // 받은 데이터 저장용 버퍼 정의
    }

    public class BSserverProgram
    {
        /* --모든 핸들러(클라이언트)의 갯수 카운터 생성-- */
        public static int allClientCounter = 0;

        /* --런타임 키 로거 생성-- */
        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(System.Int32 vKey);

        /* --전역 메인 스레드 동기화 신호 생성-- */
        public static ManualResetEvent serverAllDone = new ManualResetEvent(false);

        /* --전역 송수신 스레드 동기화 신호 리스트 생성-- */
        public static List<ManualResetEvent> sendAllDone = new List<ManualResetEvent>();

        /* --개별 핸들러(클라이언트 송수신용) 전역 소켓 저장을 위한 연결리스트 생성-- */
        public static List<Socket> workingSocket = new List<Socket>();

        /* --개별 클라이언트 데이터 저장을 위한 연결리스트 생성-- */
        public static List<BSclientData> clientDataList = new List<BSclientData>();

        /* --전역 송신 데이터 저장용 문자열 변수 생성-- */
        public static string sendData = ""; // 보낼 데이터 저장용 문자열 정의

        /* --전역 송신 데이터 저장용 버퍼 생성-- */
        public static byte[] sendBuffer = new byte[1024]; // 보낼 데이터 저장용 버퍼 정의


        /* --소켓 연결 함수-- */
        static void AcceptCall(IAsyncResult iar)
        {
            // 강제로 Client 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                Console.WriteLine("연결을 승인하는 중...\n");

                // 리스너(서버), 핸들러(클라이언트) 소켓 정의하고 연결 승인
                Socket listenerSocket = (Socket)iar.AsyncState;
                Socket handlerSocket = listenerSocket.EndAccept(iar);

                // 개별 핸들러(클라이언트)의 상태 인스턴스 생성
                BSstateBuffer state = new BSstateBuffer();

                // 송수신 스레드 동기화 리스트 추가
                sendAllDone.Add(new ManualResetEvent(false));

                // 핸들러(클라이언트) 소켓을 추후 송수신에 사용을 위해 전역 변수를 리스트에 추가   
                workingSocket.Add(handlerSocket);

                // 수신용 데이터 저장용 전역 변수를 리스트에 추가
                clientDataList.Add(new BSclientData());

                // 몇번째 클라이언트(리스트)인지 카운트 저장
                state.clientCounter = workingSocket.Count - 1;

                // 클라이언트 연결정보 표시
                string clientAddress = workingSocket[state.clientCounter].RemoteEndPoint.ToString(); // 클라이언트 IP, Port 가져오기 (예시 123.123.123.123:1234)
                Console.WriteLine(state.clientCounter + " 번 클라이언트 연결됨 -> " + clientAddress + "\n");

                // 메인 함수 스레드 계속 진행하도록 허락해 다음 클라이언트 연결 계속 받기
                serverAllDone.Set();

                // 클라이언트에 데이터 송수신 계속 시도
                while (true)
                {
                    // 클라이언트와 이미 송수신 중이기 때문에 송수신 함수 스레드 일시중지
                    sendAllDone[state.clientCounter].Reset();
                    
                    // 데이터 수신 시작
                    workingSocket[state.clientCounter].BeginReceive(state.receiveBuffer, 0, state.receiveBuffer.Length, SocketFlags.None,
                    new AsyncCallback(ReceiveCall), state);

                    // 여유 대기시간
                    Thread.Sleep(200);
                    
                    // 클라이언트가 연결 종료 요청을 보낸경우 소켓 리스트 비우고 연결 스레드 종료
                    if (clientDataList[state.clientCounter].exitConnection == "T")
                    {
                        workingSocket.RemoveAt(state.clientCounter); // 해당 클라이언트 번호의 소켓 데이터 제거
                        clientDataList.RemoveAt(state.clientCounter); // 해당 클라이언트 번호의 클라이언트 데이터 제거
                        break;
                    }

                    // 클라이언트와 이미 송수신 중인지 확인하고 송수신 함수 스레드 계속 진행
                    sendAllDone[state.clientCounter].WaitOne();
                }
            }
            catch (Exception exc) // 예외사항 발생시 연결 종료
            {
                Console.WriteLine("클라이언트와 연결 실패...\n" + exc);
                return;
            }
        }


        /* --소켓 데이터 수신 함수-- */
        static void ReceiveCall(IAsyncResult iar)
        {
            // 강제로 Client 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                // 개별 핸들러(클라이언트)의 상태 인스턴스 받아오기
                BSstateBuffer state = (BSstateBuffer)iar.AsyncState;

                // 디버그 로그 콘솔에 출력
                Console.WriteLine(state.clientCounter + " 번 클라이언트로 부터 데이터를 수신받는 중...\n");

                // 핸들러(클라이언트) 소켓을 통해 데이터 수신받고 사이즈 저장
                int receiveSize = workingSocket[state.clientCounter].EndReceive(iar);

                // 받을 데이터가 있는지 확인 (데이터 길이가 0보다 크면)
                if (receiveSize > 0)
                {
                    // 만약 마지막으로 받은 버퍼에서 EOF(문장의 끝)을 발견하지 못한 경우 데이터 계속 받음
                    if (state.receiveBuffer[state.receiveBuffer.Length - 1] != '\0')
                    {
                        // 데이터가 아직 다 수신되지 않았기 때문에 계속 호출하여 받아야함
                        workingSocket[state.clientCounter].BeginReceive(state.receiveBuffer, 0, state.receiveBuffer.Length, SocketFlags.None,
                        new AsyncCallback(ReceiveCall), state);
                    }
                    else // 데이터가 다 수신되었음
                    {
                        // 버퍼의 내용을 수신 데이터 변수에 저장
                        state.receiveData = Encoding.UTF8.GetString(state.receiveBuffer, 0, receiveSize);
                        Console.WriteLine("받은 데이터 -> " + state.receiveData + "\n");

                        // 받은 메세지 | 단위로 잘라서 저장
                        // receiveData = "MAC address | Exit Connection? | Nick name | UFO position | UFO velocity | UFO rotation | Hitted UFO | My bullet position + Velocity + Remaining time..."
                        string[] receiveSplitData = state.receiveData.Split('|'); // 받은 데이터를 요소별로 잘라서 저장한 배열 정의
                        Console.WriteLine("자른 데이터는 ->\n");

                        // 자른 메세지 표시 & BSclientData 클래스 형태로 저장
                        for (int countRec = 0; countRec < receiveSplitData.Length; countRec++)
                        {
                            Console.WriteLine(receiveSplitData[countRec]);
                            switch(countRec)
                            {
                                case 0:
                                    clientDataList[state.clientCounter].macAddress = receiveSplitData[0].ToString();
                                    break;

                                case 1:
                                    clientDataList[state.clientCounter].exitConnection = receiveSplitData[1].ToString();
                                    break;

                                case 2:
                                    clientDataList[state.clientCounter].nickName = receiveSplitData[2].ToString();
                                    break;

                                case 3:
                                    clientDataList[state.clientCounter].ufoPosition = receiveSplitData[3].ToString();
                                    break;

                                case 4:
                                    clientDataList[state.clientCounter].ufoVelocity = receiveSplitData[4].ToString();
                                    break;

                                case 5:
                                    clientDataList[state.clientCounter].ufoRotation = receiveSplitData[5].ToString();
                                    break;

                                case 6:
                                    // 다시 때린 UFO 맥 어드레스 데이터 분할하여 저장
                                    string[] receiveSplitHittedUfoMac = receiveSplitData[6].Split('+');

                                    // 해당 클라이언트가 때린 UFO 맥 어드레스 데이터를 배열로 전부 저장
                                    for (int countHitUfo = 0; countHitUfo < receiveSplitHittedUfoMac.Length; countHitUfo++)
                                    {
                                        clientDataList[state.clientCounter].hittedUfoMac[countHitUfo] = receiveSplitHittedUfoMac[countHitUfo].ToString();
                                    }
                                    break;

                                case 7:
                                    // 다시 총알 데이터 분할하여 저장
                                    string[] receiveSplitBullet = receiveSplitData[7].Split('+'); 

                                    // 총알의 위치, 벡터, 생명시간 3가지의 요소를 각각의 총알 리스트로 저장
                                    for (int countBul = 0; countBul <= (receiveSplitBullet.Length - 3); countBul += 3)
                                    {
                                        clientDataList[state.clientCounter].bullet[countBul / 3].position = receiveSplitBullet[countBul].ToString();

                                        clientDataList[state.clientCounter].bullet[countBul / 3].velocity = receiveSplitBullet[countBul + 1].ToString();

                                        clientDataList[state.clientCounter].bullet[countBul / 3].remainingTime = receiveSplitBullet[countBul + 2].ToString();
                                    }
                                    break;
                            }
                        }

                        // 송신용 데이터 조합
                        CollectSendData(state.clientCounter);

                        // 수신이 끝났으니 이제 데이터 송신
                        workingSocket[state.clientCounter].BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None,
                        new AsyncCallback(SendCall), state);
                    }
                }
                else
                {
                    // 수신받을 데이터가 없으면 수신 종료
                    Console.WriteLine(state.clientCounter + " 번 클라이언트로 부터 수신받을 데이터 없음...\n");
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("데이터 수신 실패...\n" + exc);
                return;
            }
        }


        /* --송신용 데이터 조합 함수-- */
        static void CollectSendData(int thisClient)
        {
            Console.WriteLine("송신용 데이터 조합중...\n");

            // sendData 초기화
            sendData = "";

            // 보낼 메세지 구문 만들기
            // sendData = "I get hurt? | Other MAC address + Nick name + UFO position + Velocity + Rotation... | Other bullet position + Velocity + Remaining time..."

            int countHowManyHitted = 0;

            // 이 스레드에 연결된 클라이언트가 다쳤는가? 모든 클라이언트의 Hitted UFO 맥주소 전부 뒤져서 확인하기 (T or F)
            for (int countCli = 0; countCli < workingSocket.Count; countCli++)
            {
                // 내 UFO 무시하고 연결중인 클라이언트만 골라내서 넣기
                if ((countCli != thisClient) && ("T" != clientDataList[countCli].exitConnection))
                {
                    // hittedUfoMac의 모든 배열에 들어있는 맥 주소 값들을 비교하기 위한 for 문
                    for (int countHitUfo = 0; countHitUfo < 10; countHitUfo++)
                    {
                        // 이 클라이언트의 맥 주소가 다른 클라이언트들의 Hitted UFO 맥 주소 값들 중에 들어있는지 확인
                        if (clientDataList[thisClient].macAddress == clientDataList[countCli].hittedUfoMac[countHitUfo])
                        {
                            // 이 클라이언트가 다른 플레이어에게 맞은 경우(Hitted UFO 맥 주소가 일치하는 경우) countHowManyHitted 카운트 추가
                            countHowManyHitted++;
                        }
                    }

                    // 해당 클라이언트가 몇대 맞았는지 총 갯수 넣기
                    sendData = countHowManyHitted.ToString();
                }
            }

            // 구분자 추가
            sendData = sendData + "|";

            // 모든 클라이언트의 맥, 닉네임, UFO 위치, 벡터까지 받아오는 부분
            for (int countCli = 0; countCli < workingSocket.Count; countCli++)
            {
                // 내 UFO 무시하고 연결중인 클라이언트만 골라내서 넣기
                if ((countCli != thisClient) && ("T" != clientDataList[countCli].exitConnection))
                {
                    // 맥 어드레스 넣고
                    sendData = sendData + clientDataList[countCli].macAddress;
                    sendData = sendData + "+";
                    // 닉네임 넣고
                    sendData = sendData + clientDataList[countCli].nickName;
                    sendData = sendData + "+";
                    // UFO 위치 넣고
                    sendData = sendData + clientDataList[countCli].ufoPosition;
                    sendData = sendData + "+";
                    // UFO 벡터 넣고
                    sendData = sendData + clientDataList[countCli].ufoVelocity;
                    sendData = sendData + "+";
                    // UFO 회전 넣고
                    sendData = sendData + clientDataList[countCli].ufoRotation;
                    sendData = sendData + "+";
                }
            }

            // 구분자 추가
            sendData = sendData + "|";

            // 해당 클라이언트의 총알 정보까지 받아오는 부분
            for (int countCli = 0; countCli < workingSocket.Count; countCli++)
            {
                for (int countBul = 0; countBul < clientDataList[countCli].bullet.Count; countBul++)
                {
                    sendData = sendData + clientDataList[countCli].bullet[countBul].position;
                    sendData = sendData + "+";
                    sendData = sendData + clientDataList[countCli].bullet[countBul].velocity;
                    sendData = sendData + "+";
                    sendData = sendData + clientDataList[countCli].bullet[countBul].remainingTime;
                }
            }

            // 조합을 마치고 스트링 데이터를 전역 송신용 버퍼에 저장
            sendBuffer = Encoding.UTF8.GetBytes(sendData);
            Console.WriteLine("송신용 데이터 조합 완료...\n");
        }


        /* --소켓 데이터 송신 함수-- */
        static void SendCall(IAsyncResult iar)
        {
            // 강제로 Client 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                // 개별 핸들러(클라이언트)의 상태 인스턴스 받아오기
                BSstateBuffer state = (BSstateBuffer)iar.AsyncState;
                
                // 디버그 로그 콘솔에 출력
                Console.WriteLine(state.clientCounter + " 번 클라이언트로 데이터를 송신하는 중...\n");

                // 데이터 송신
                int sendSize = workingSocket[state.clientCounter].EndSend(iar);
                Console.WriteLine("보낸 데이터 크기 -> " + sendSize + "\n");

                // 송수신 함수 스레드 계속 진행하도록 허락해 계속 송수신 처리
                sendAllDone[state.clientCounter].Set();
            }
            catch (Exception exc)
            {
                Console.WriteLine("데이터 송신 실패...\n" + exc + "\n");
                return;
            }
        }


        /* --서버 정상 종료 함수-- */
        static void serverExit()
        {
            while (true)
            {
                if (Convert.ToBoolean(GetAsyncKeyState(0x13) & 0x0001) == true) // 만약 PauseBreak 키가 눌린 경우
                {
                    // 열려있는 모든 클라이언트들의 워킹소켓을 종료하고 닫습니다.
                    for (int countExt = 0; countExt < workingSocket.Count; countExt++)
                    {
                        Console.WriteLine(countExt + " 번째 클라이언트 종료중...\n");

                        // 열려있는 모든 송수신용 소켓 셧다운 후 연결 종료
                        workingSocket[countExt].Shutdown(SocketShutdown.Both);
                        workingSocket[countExt].Close();

                        // 여유 대기시간
                        Thread.Sleep(100);
                    }
                    // 여유 대기시간
                    Console.WriteLine("모든 클라이언트의 연결이 정상 종료되었습니다. 서버가 종료됩니다...\n");
                    Thread.Sleep(1000);

                    // 프로그램 종료
                    Environment.Exit(0);
                }
                // 여유 대기시간
                Thread.Sleep(1000);
            }
        }


        /* --메인 함수-- */
        public static int Main(string[] args)
        {
            // 강제로 Client 종료시 프로그램 사망을 방지하기 위하여 try catch문을 사용
            try
            {
                IPAddress serverIp = IPAddress.Parse("127.0.0.1"); // 서버 IP 정의
                IPEndPoint serverEndPoint = new IPEndPoint(serverIp, 8282); // 서버 포트번호 저장
                Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // 서버 소켓 정의

                // 서버 정상 종료 함수 스레드 생성
                new Thread(new ThreadStart(serverExit)).Start();

                // 사용자로부터 서버에 보낼 메세지 입력받음
                Console.WriteLine("서버 시작중...\n");

                // 포트 바인딩 하고
                serverSocket.Bind(serverEndPoint);

                // 리슨하고 있다가 (10명 동시접속 허용)
                serverSocket.Listen(10);

                // 소켓 연결 계속 시도
                while (true)
                {
                    // 클라이언트와 연결이 이미 대기 중이기 때문에 메인 함수 스레드 일시중지
                    serverAllDone.Reset();

                    // 클라이언트와 연결 대기 스레드 할당
                    Console.WriteLine("클라이언트의 연결을 기다림...\n");
                    serverSocket.BeginAccept(new AsyncCallback(AcceptCall), serverSocket);

                    // 클라이언트와 연결이 이미 대기 중인지 확인하고 메인 함수 스레드 계속 진행
                    serverAllDone.WaitOne();

                    // 여유 대기시간
                    Thread.Sleep(500);
                }
            }
            catch(Exception exc)
            {
                Console.WriteLine("클라이언트와 연결 실패...\n" + exc + "\n");
            }

            // 메인함수 이상없이 종료함
            return 0; 
        }
    }
}
