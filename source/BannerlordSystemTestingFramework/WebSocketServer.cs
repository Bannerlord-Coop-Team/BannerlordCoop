using SuperWebSocket;

namespace BannerlordSystemTestingLibrary
{
    class WebSocketServer
    {
        private static SuperWebSocket.WebSocketServer instance;

        public static SuperWebSocket.WebSocketServer Instance { 
            get {
                if(instance == null)
                {
                    instance = new SuperWebSocket.WebSocketServer();
                    int port = 8080;
                    instance.Setup(port);
                    instance.Start();
                }
                
                return instance; 
            } 
        }
    }
}
