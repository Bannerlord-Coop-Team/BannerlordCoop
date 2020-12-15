using SimpleTCP;
using System;
using System.Net;

namespace BannerlordSystemTestingLibrary
{
    public class SocketServer
    {
        private static SimpleTcpServer instance;
        public static SimpleTcpServer Instance { 
            get {
                if(instance == null)
                {
                    instance = new SimpleTcpServer();
                }
                
                return instance; 
            } 
        }
    }
}
