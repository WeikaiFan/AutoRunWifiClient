using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Win32;
using System.Timers;

namespace AutoRunservice
{
    class Program
    {
        private static string _ftpServerIP = "www.ctob.info";       //FTP来源應用程式 Server IP
        private static string _ftpPath = "WiFi/WiFi_Client/bin";       //FTP来源應用程式路径
        private static string _ftpUserID = "WifiUser";                //FTP User ID
        private static string _ftpPassword = "User@0001";          //FTP User Password


        private static string _appPath = "C:\\WiFi\\WiFi_Client\\bin";  //應用程式路径     
        private static string _appFile = "wifi_探针读取.EXE";                  //應用程式檔名   
        private static string _registryKeyName = "WiFi_Read";                 //登錄檔名稱    
        private static string _processName = "wifi_探针读取";                  //执行程序名稱
        private static double _runTimeInterval = 100000;                       //Timer Interval

        static void Main(string[] args)
        {
            ProcessMonitor processMonitor = new ProcessMonitor();
            processMonitor.AutoRun(_ftpServerIP, _ftpPath, _ftpUserID, _ftpPassword, _appPath, _appFile, _registryKeyName, _processName, _runTimeInterval);
            //Console.ReadLine();
        }
    }
}
