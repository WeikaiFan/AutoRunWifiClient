using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Timers;
using System.IO;
using System.Net;

namespace AutoRunservice
{
    public class ProcessMonitor
    {
        #region 变数设定

        private string _ftpServerIP = "";        //FTP来源應用程式 Server IP
        private string _ftpPath = "";           //FTP来源應用程式路径
        private string _ftpUserID = "";                //FTP User ID
        private string _ftpPassword = "";          //FTP User Password

        private string _appPath = "";           //應用程式路径    
        private string _appFile = "";           //應用程式檔名  
        private string _appPathFile = "";       //應用程式路径檔名   

        //登錄檔路径及名稱
        private string _registryKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private string _registryKeyName = "";   //登錄檔名稱 

        private string _processName = "";       //执行程序名稱

        private System.Timers.Timer autoTimer;
        private double _runTimeInterval = 10000;

        #endregion

        #region AutoRun

        public void AutoRun(string ftpServerIP, string ftpPath, string ftpUserID, string ftpPassword, string appPath, string appFile, string registryKeyName, string processName, double runTimeInterval)
        {
            _ftpServerIP = ftpServerIP;
            _ftpPath = ftpPath;
            _ftpUserID = ftpUserID;
            _ftpPassword = ftpPassword;
            _appPath = appPath;
            _appFile = appFile;
            _registryKeyName = registryKeyName;
            _processName = processName;
            _appPathFile = _appPath + "\\" + _appFile;
            _runTimeInterval = runTimeInterval;

            AutoRun();
        }

        public void AutoRun()
        {
            AutoRunProcess();
            SetTimer();

            autoTimer.Stop();
            autoTimer.Dispose();

            Console.WriteLine("Terminating the application...");
        }

        private void SetTimer()
        {
            autoTimer = new System.Timers.Timer(_runTimeInterval);
            autoTimer.Elapsed += OnTimedEvent;
            autoTimer.AutoReset = true;
            autoTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                AutoRunProcess();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnTimedEvent 寫入失敗:" + ex.Message);
            }
            Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}",
                                      e.SignalTime);
        }

        private void AutoRunProcess()
        {
            try
            {
                //检查應用程式路径档案
                if (!File.Exists(_appPathFile))
                {
                    DownloadFtpFiles(_ftpServerIP, _ftpPath);
                    Console.WriteLine("Copy Ftp Done!");
                }
                else
                {
                    //检查應用程式的时间与服务器的时间是否一致
                    DateTime serverAppCreatedTime = GetFtpFileCreatedTime(_ftpServerIP, _ftpPath, _appFile);    //Server Ftp App File time
                    DateTime localAppCreatedTime = File.GetLastWriteTime(_appPathFile);  //Local App File time
                    if (serverAppCreatedTime > localAppCreatedTime)
                    {
                        Process process = GetProcess(_processName);
                        //先删除执行中的程序
                        if (process != null)
                        {
                            process.Kill();
                            Console.WriteLine("Kill app process!");
                        }
                        //从服务器上下载新的應用程式
                        DownloadFtpFiles(_ftpServerIP, _ftpPath);
                        Console.WriteLine("Copy AutoRun!!");
                    }
                    else
                    {
                        //登錄檔在开机自动执行注册應用程式
                        if (!CheckRegistAutoRun(_registryKeyName))
                        {
                            RegistAutoRun(_registryKeyName, _appPathFile);
                            Console.WriteLine("Regist AutoRun!");
                        }

                        //执行登錄檔在开机自动执行内注册的應用程式
                        if (GetProcess(_processName) == null)
                        {
                            ReStartAutoRun(_registryKeyName);
                            Console.WriteLine("ReStart AutoRun application!");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("OnTimedEvent 寫入失敗:" + ex.ToString());
            }
        }

        #endregion

        #region Copy Ftp Files 

        private DateTime GetFtpFileCreatedTime(string ftpServerIP, string ftpPath, string fileName)
        {
            string uri = "ftp://" + ftpServerIP + "/" + ftpPath + "/" + fileName;
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;
            request.Credentials = new NetworkCredential(_ftpUserID, _ftpPassword);
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            DateTime ftpFileCreatedTime = response.LastModified;
            return ftpFileCreatedTime;
        }

        private bool CompareAppCreatedTime(DateTime serverAppCreatedTime, DateTime localAppCreatedTime)
        {
            bool isMatched = false;
            if (string.Compare(serverAppCreatedTime.ToString(), localAppCreatedTime.ToString()) == 0)
                isMatched = true;
            else
                isMatched = false;
            return isMatched;
        }

        public void DownloadFtpFiles(string ftpServerIP, string ftpPath)
        {
            try
            {
                string uriPath = "ftp://" + ftpServerIP + "/" + ftpPath + "/";
                // Get the object used to communicate with the server.
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uriPath);
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                request.Credentials = new NetworkCredential(_ftpUserID, _ftpPassword);
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);

                while (!reader.EndOfStream)
                {
                    String fileAttribute = reader.ReadLine();
                    int idxLastSpace = fileAttribute.LastIndexOf(" ");
                    String fileName = fileAttribute.Substring(idxLastSpace + 1);

                    string destPathFile = _appPath + "\\" + fileName;

                    //if (!File.Exists(destPathFile))
                    //{
                        DownloadFile(ftpServerIP, ftpPath, fileName);
                    //}
                }

                Console.WriteLine("Directory List Complete, status {0}", response.StatusDescription);

                reader.Close();
                response.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine("AutoRunProcess 执行失敗:" + ex.Message);
            }
        }

        public void DownloadFile(string ftpServerIP, string ftpPath, string fileName)
        {
            try
            {
                string uri = "ftp://" + ftpServerIP + "/" + ftpPath + "/" + fileName;
                Uri serverUri = new Uri(uri);
                if (serverUri.Scheme != Uri.UriSchemeFtp)
                {
                    return;
                }

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(_ftpUserID, _ftpPassword);
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();

                if (!System.IO.Directory.Exists(_appPath))
                {
                    System.IO.Directory.CreateDirectory(_appPath);
                }

                FileStream writeStream = new FileStream(_appPath + "\\" + fileName, FileMode.Create);                
                int Length = 2048;
                Byte[] buffer = new Byte[Length];
                int bytesRead = responseStream.Read(buffer, 0, Length);
                while (bytesRead > 0)
                {
                    writeStream.Write(buffer, 0, bytesRead);
                    bytesRead = responseStream.Read(buffer, 0, Length);
                }
                Console.WriteLine("Copy File:" + fileName + "is Done!");
                writeStream.Close();
                response.Close();
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message, "Download Error");
            }
            catch (Exception ex1)
            {
                Console.WriteLine(ex1.Message, "Download Error1");
            }
        }

        #endregion

        #region Copy Local Path Files

        public void CopyPathFiles(string sourcePath, string appPath)
        {
            try
            {
                if (!System.IO.Directory.Exists(appPath))
                {
                    System.IO.Directory.CreateDirectory(appPath);
                }

                if (System.IO.Directory.Exists(sourcePath))
                {
                    string[] files = System.IO.Directory.GetFiles(sourcePath);

                    foreach (string s in files)
                    {
                        string fileName = System.IO.Path.GetFileName(s);
                        string destFile = System.IO.Path.Combine(appPath, fileName);
                        if (!System.IO.File.Exists(destFile))
                            System.IO.File.Copy(s, destFile, true);
                        else
                            Console.WriteLine("dest File is exist!");
                    }
                }
                else
                {
                    Console.WriteLine("Source path does not exist!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("寫入失敗:" + ex.Message);
            }
        }

        #endregion

        #region 登錄开机自动执行应用程序

        public bool CheckRegistAutoRun(string registryKeyName)
        {
            bool isExist = false;
            try
            {
                //開啟登錄檔位置，這個位置是存放啟動應用程式的地方
                RegistryKey aimdir = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true);
                //若登錄檔已經存在則刪除
                if (aimdir.GetValue(registryKeyName) != null)
                {
                    isExist = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("登錄檔寫入失敗:" + ex.Message);
            }
            return isExist;
        }

        //预设在應用程式的位置
        public void RegistAutoRun(string registryKeyName)
        {
            try
            {
                //宣告登錄檔名稱 string appName = "WiFi_Read";
                //選宣告一個字串表示本身應用程式的位置後面加的是參數"-s",若沒有附帶啟動參數的話可以不加
                //string startPath = Application.ExecutablePath + " -S";
                string startPath = Application.ExecutablePath;
                RegistAutoRun(registryKeyName, startPath);

            }
            catch (Exception ex)
            {
                Console.WriteLine("登錄檔寫入失敗:" + ex.Message);
            }
        }

        public void RegistAutoRun(string registryKeyName, string appPathFile)
        {
            try
            {
                //開啟登錄檔位置，這個位置是存放啟動應用程式的地方
                RegistryKey aimdir = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true);
                //若登錄檔已經存在則刪除
                if (aimdir.GetValue(registryKeyName) != null)
                {
                    //刪除
                    aimdir.DeleteValue(registryKeyName, false);
                }
                //寫入登錄檔值
                aimdir.SetValue(registryKeyName, appPathFile);
                //關閉登錄檔
                aimdir.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine("登錄檔寫入失敗:" + ex.Message);
            }
        }

        #endregion

        #region 启动在登錄档内的开机自动执行应用程序

        public void ReStartAutoRun(string registryKeyName)
        {
            string strWorkingDirectory = "";
            string strFileName = "";

            //開啟登錄檔位置，這個位置是存放啟動應用程式的地方
            RegistryKey aimdir = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true);
            //若登錄檔已經存在
            if (aimdir.GetValue(registryKeyName) != null)
            {
                var keyValue = aimdir.GetValue(registryKeyName);
                string FullName = keyValue.ToString();
                int idxLastPath = FullName.LastIndexOf("\\");
                strWorkingDirectory = FullName.Substring(0, idxLastPath + 1);
                int idxEXE = FullName.ToUpper().LastIndexOf("EXE");
                strFileName = FullName.Substring(idxLastPath + 1, idxEXE + 1 - idxLastPath + 1);

                StartProcess(strWorkingDirectory, strFileName);
            }
        }

        #endregion

        #region 检查应用程序

        public Process GetProcess(string processName)
        {
            Process process = null;
            //bool isProcess = false;

            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.Contains(processName))
                {
                    process = clsProcess;
                    break;
                }
            }
            return process;
        }

        #endregion

        #region 启动应用程序

        private void StartProcess(string strWorkingDirectory, string strFileName)
        {
            ProcessStartInfo _processStartInfo = new ProcessStartInfo();
            _processStartInfo.WorkingDirectory = strWorkingDirectory;
            _processStartInfo.FileName = strFileName;
            _processStartInfo.CreateNoWindow = true;
            Process myProcess = Process.Start(_processStartInfo);
        }

        #endregion
    }
}
